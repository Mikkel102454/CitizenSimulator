using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Modding;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Definitions;
using CitizenSimulator.CitizenMind.Configuration;
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

        MindDefinitionLoader.Load(mind, typeof(EmploymentActions).Assembly);
    }

    public static double GetCommuteMinutes(IEntityStore store, EntityRef citizen)
    {
        double minutes = store.GetComponent<WorkerProfileComponent>(citizen)?.CommuteMinutes ?? DefaultCommuteMinutes;
        return double.IsFinite(minutes) && minutes > 0 ? minutes : DefaultCommuteMinutes;
    }
}
