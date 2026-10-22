using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Models;
using CitizenSimulator.CitizenMind.Services.Abstractions;

namespace CitizenSimulator.Business.Models.CitizenMind;

public static class EmploymentActions
{
    public const string FindJobGoal = "business.find_job";
    public const string AttendShiftGoal = "business.attend_shift";
    public const string ResignGoal = "business.leave_unpaid_job";
    public const string AcceptJobAction = "business.accept_job";
    public const string TravelAction = "business.travel_to_work";
    public const string WorkAction = "business.work_shift";
    public const string ResignAction = "business.resign";
    public const string JobProvider = "business.jobs";
    public const string ShiftProvider = "business.shifts";
    public const string HasJobFact = "employment.hasJob";
    public const string CanFindJobFact = "employment.canFindJob";
    public const string AtWorkplaceFact = "employment.atWorkplace";
    public const string ShiftDueFact = "employment.shiftDue";
    public const string ShouldResignFact = "employment.shouldResign";
    public const string CommuteMinutesFact = "employment.commuteMinutes";
    public const string CompletedShiftFact = "employment.completedShiftEndMinutes";
    public const double ApplicationMinutes = 5;
    public const double DefaultCommuteMinutes = 15;

    public static void Register(IModContext context)
    {
        var mind = context.Services.Get<ICitizenMindService>();
        var businesses = context.Services.Get<IBusinessService>();
        var calendar = context.Services.Get<ICalenderService>();

        CitizenComponent? GetWorker(EntityRef entity)
        {
            return context.Store.IsAlive(entity) ? context.Store.GetComponent<CitizenComponent>(entity) : null;
        }

        Employment? GetEmployment(EntityRef entity)
        {
            var worker = GetWorker(entity);
            return worker == null ? null : businesses.GetEmployment(worker);
        }

        WorkShift? GetShift(EntityRef entity)
        {
            var worker = GetWorker(entity);
            return worker == null ? null : businesses.GetNextShift(worker);
        }

        mind.RegisterFact(HasJobFact, entity => GetEmployment(entity) == null ? 0.0 : 1.0, canPredict: true);
        mind.RegisterFact(CanFindJobFact, entity =>
        {
            var worker = GetWorker(entity);
            return worker != null && businesses.GetAvailableJobs(worker).Count > 0 ? 1.0 : 0.0;
        });
        mind.RegisterFact(AtWorkplaceFact, entity =>
            GetEmployment(entity)?.Contract.IsAtWorkplace == true ? 1.0 : 0.0, canPredict: true);
        mind.RegisterFact(CommuteMinutesFact, entity => GetCommuteMinutes(context.Store, entity));
        mind.RegisterFact(CompletedShiftFact,
            entity => GetEmployment(entity)?.Contract.LastCompletedShiftEnd.TotalMinutes ?? 0, canPredict: true);
        mind.RegisterFact(ShiftDueFact, entity =>
        {
            var shift = GetShift(entity);
            if (shift == null)
                return 0.0;

            // Allow one action boundary in addition to the commute so work can start on time.
            var preparation = TimeSpan.FromMinutes(GetCommuteMinutes(context.Store, entity)) + calendar.GetGameTime(1);
            var now = businesses.GameTime;
            return shift.Start <= now + preparation ? 1.0 : 0.0;
        }, canPredict: true);
        mind.RegisterFact(ShouldResignFact, entity =>
        {
            var employment = GetEmployment(entity);
            return employment is { Business.State: BusinessState.Suspended, Contract.UnpaidWages: > 0 } ? 1.0 : 0.0;
        });

        mind.RegisterCandidateProvider(new CandidateProviderDescriptor
        {
            Id = JobProvider,
            BindingTypes = new Dictionary<string, Type>
            {
                ["employer"] = typeof(EntityRef),
                ["role"] = typeof(string),
                ["hourlyWage"] = typeof(decimal)
            }
        }, entity => new JobCandidateProvider(GetWorker(entity) is { } worker
            ? businesses.GetAvailableJobs(worker)
            : []));
        mind.RegisterCandidateProvider(new CandidateProviderDescriptor
        {
            Id = ShiftProvider,
            BindingTypes = new Dictionary<string, Type>
            {
                ["employer"] = typeof(EntityRef),
                ["contractId"] = typeof(Guid),
                ["shiftStartMinutes"] = typeof(double),
                ["shiftEndMinutes"] = typeof(double)
            }
        }, entity => new ShiftCandidateProvider(GetWorker(entity) is { } worker
            ? businesses.GetUpcomingShifts(worker) : [], GetCommuteMinutes(context.Store, entity), calendar.GetGameTime(1)));

        mind.RegisterHandler(AcceptJobAction, new AcceptJobHandler(context.Store, businesses));
        mind.RegisterHandler(TravelAction, new TravelToWorkHandler(context.Store, businesses));
        mind.RegisterHandler(WorkAction, new WorkShiftHandler(context.Store, businesses));
        mind.RegisterHandler(ResignAction, new ResignJobHandler(context.Store, businesses));

        foreach (var action in CreateActions())
            mind.RegisterAction(action);

        mind.RegisterGoal(new GoalDefinition
        {
            Id = FindJobGoal,
            Eligible = GreaterThanZero(CanFindJobFact),
            Desired = [GreaterThanZero(HasJobFact)],
            Utility = CreateUtility(0.35),
            GoalActions = [AcceptJobAction]
        });
        mind.RegisterGoal(new GoalDefinition
        {
            Id = AttendShiftGoal,
            Eligible = GreaterThanZero(ShiftDueFact),
            Desired = [IsZero(ShiftDueFact)],
            // Confirmed occurrences are scheduled commitments; needs emergencies still take priority.
            Utility = CreateUtility(0.8),
            GoalActions = [TravelAction, WorkAction]
        });
        mind.RegisterGoal(new GoalDefinition
        {
            Id = ResignGoal,
            Eligible = GreaterThanZero(ShouldResignFact),
            Desired = [IsZero(HasJobFact)],
            Utility = CreateUtility(0.7),
            GoalActions = [ResignAction]
        });
    }

    public static IReadOnlyList<ActionDefinition> CreateActions()
    {
        var clock = new ExpressionDefinition { Time = "totalMinutes" };
        var shiftEnd = new ExpressionDefinition { Binding = "shiftEndMinutes" };
        return
        [
            new ActionDefinition
            {
                Id = AcceptJobAction,
                Handler = AcceptJobAction,
                CandidateProvider = JobProvider,
                Preconditions = [IsZero(HasJobFact)],
                Duration = Minutes(Literal(ApplicationMinutes)),
                Prediction = [SetFact(HasJobFact, 1)],
                Completion = [GreaterThanZero(HasJobFact)],
                Cost = new CostDefinition
                {
                    Expression = Op("add", Literal(ApplicationMinutes),
                        Op("divide", Literal(1000), new ExpressionDefinition { Binding = "hourlyWage" }))
                },
                Execution = new ExecutionDefinition { Interruptible = true, TimeoutMinutes = 30 }
            },
            new ActionDefinition
            {
                Id = TravelAction,
                Handler = TravelAction,
                CandidateProvider = ShiftProvider,
                Preconditions = [GreaterThanZero(HasJobFact), IsZero(AtWorkplaceFact)],
                Duration = Minutes(Fact(CommuteMinutesFact)),
                Prediction = [SetFact(AtWorkplaceFact, 1)],
                Completion = [GreaterThanZero(AtWorkplaceFact)],
                Cost = DurationCost(),
                Execution = new ExecutionDefinition { Interruptible = true }
            },
            new ActionDefinition
            {
                Id = WorkAction,
                Handler = WorkAction,
                CandidateProvider = ShiftProvider,
                Preconditions = [GreaterThanZero(HasJobFact), GreaterThanZero(AtWorkplaceFact)],
                Duration = Minutes(Op("subtract", shiftEnd, clock)),
                Prediction =
                [
                    SetFact(ShiftDueFact, 0), SetFact(AtWorkplaceFact, 0),
                    new PredictionEffectDefinition { Fact = CompletedShiftFact, Op = "set", Value = shiftEnd }
                ],
                Completion = [Op("gte", Fact(CompletedShiftFact), shiftEnd), IsZero(AtWorkplaceFact)],
                Cost = DurationCost(),
                Execution = new ExecutionDefinition { Interruptible = true }
            },
            new ActionDefinition
            {
                Id = ResignAction,
                Handler = ResignAction,
                Preconditions = [GreaterThanZero(HasJobFact), GreaterThanZero(ShouldResignFact)],
                Duration = Minutes(Literal(ApplicationMinutes)),
                Prediction = [SetFact(HasJobFact, 0), SetFact(AtWorkplaceFact, 0), SetFact(ShiftDueFact, 0)],
                Completion = [IsZero(HasJobFact)],
                Cost = DurationCost(),
                Execution = new ExecutionDefinition { Interruptible = true, TimeoutMinutes = 30 }
            }
        ];
    }

    public static double GetCommuteMinutes(IEntityStore store, EntityRef citizen)
    {
        double minutes = store.GetComponent<WorkerProfileComponent>(citizen)?.CommuteMinutes ?? DefaultCommuteMinutes;
        return double.IsFinite(minutes) && minutes > 0 ? minutes : DefaultCommuteMinutes;
    }

    private static ExpressionDefinition Fact(string id)
    {
        return new ExpressionDefinition { Fact = id };
    }

    private static UtilityDefinition CreateUtility(double bias)
    {
        return new UtilityDefinition
        {
            Bias = bias,
            Considerations =
            [
                new ConsiderationDefinition { Input = Literal(1), Curve = [[0, 0], [1, 1]] }
            ]
        };
    }

    private static ExpressionDefinition Literal(double value)
    {
        return new ExpressionDefinition { Literal = value };
    }

    private static ExpressionDefinition Op(string operation, ExpressionDefinition left, ExpressionDefinition right)
    {
        return new ExpressionDefinition { Op = operation, Args = [left, right] };
    }

    private static ExpressionDefinition GreaterThanZero(string fact)
    {
        return Op("gt", Fact(fact), Literal(0));
    }

    private static ExpressionDefinition IsZero(string fact)
    {
        return Op("lte", Fact(fact), Literal(0));
    }

    private static PredictionEffectDefinition SetFact(string fact, double value)
    {
        return new PredictionEffectDefinition { Fact = fact, Op = "set", Value = Literal(value) };
    }

    private static DurationDefinition Minutes(ExpressionDefinition estimate)
    {
        return new DurationDefinition { Unit = "simulationMinutes", Estimate = estimate };
    }

    private static CostDefinition DurationCost()
    {
        return new CostDefinition { Expression = new ExpressionDefinition { Calculated = "durationMinutes" } };
    }
}
