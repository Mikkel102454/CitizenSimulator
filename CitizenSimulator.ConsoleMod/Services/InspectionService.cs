using System.Globalization;
using CitizenSimulator.Abstractions.ECS;
using CitizenSimulator.Abstractions.Services;
using CitizenSimulator.Abstractions.Timing;
using CitizenSimulator.Business.Components;
using CitizenSimulator.Business.Models;
using CitizenSimulator.Business.Services.Abstractions;
using CitizenSimulator.Calender.Services.Abstractions;
using CitizenSimulator.Citizen.Components;
using CitizenSimulator.CitizenMind.Components;
using CitizenSimulator.CitizenMind.Scheduling;
using CitizenSimulator.CitizenMind.Services.Abstractions;
using CitizenSimulator.CitizenMind.Systems;
using CitizenSimulator.Housing.Components;
using CitizenSimulator.Housing.Services.Abstractions;
using CitizenSimulator.Inventory.Components;
using CitizenSimulator.Needs.Components;
using CitizenSimulator.Property.Components;
using CitizenSimulator.Vault.Components;

namespace CitizenSimulator.ConsoleMod.Services;

internal sealed class InspectionService(IEntityStore store, TextWriter output, ISimulationClock clock,
    IServiceRegistry? services, ISystemRegistry? systems)
{
    private const string Usage = "inspect <citizen-id> | inspect citizen|business|house|property <id> | " +
        "inspect citizens|businesses|houses|properties (citizens use citizen IDs; other types use entity IDs)";

    public void Execute(string[] args)
    {
        if (args.Length == 1)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "citizens":
                    List("Citizens (citizen ID / entity ID)", All<CitizenComponent>().OrderBy(c => c.Id)
                        .Select(c => $"{Person(c)} | entity {c.Parent.Id} | age {c.Age} | {(c.Alive ? "alive" : "dead")}"));
                    return;
                case "businesses":
                    List("Businesses (entity IDs)", All<BusinessComponent>().Select(b =>
                        $"{b.Parent.Id}: {b.Name} | {b.Type.Id} | {b.State} | issue {b.Issue} | owner {Person(b.Owner)}"));
                    return;
                case "houses":
                    List("Houses (entity IDs)", All<HouseComponent>().Select(h =>
                        $"{h.Parent.Id}: {h.Property.Type} | occupants {h.Occupants.Count}/{h.MaxOccupants} | owner {Person(h.Property.Owner)}"));
                    return;
                case "properties":
                    List("Properties (entity IDs)", All<PropertyComponent>().Select(p =>
                        $"{p.Parent.Id}: {p.Type} | area {N(p.AreaM2)} m2 | condition {N(p.Condition)}% | owner {Person(p.Owner)}"));
                    return;
                case "help": output.WriteLine(Usage); return;
            }
        }

        string type = args.Length == 1 ? "citizen" : args.Length == 2 ? args[0].ToLowerInvariant() : "";
        string idText = args.Length > 0 ? args[^1] : "";
        if (!uint.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0)
        {
            output.WriteLine("Invalid inspection command or ID. " + Usage);
            return;
        }
        switch (type)
        {
            case "citizen":
                var citizen = All<CitizenComponent>().FirstOrDefault(c => c.Id == id);
                if (citizen == null) Missing(type, id); else Citizen(citizen);
                break;
            case "business":
                var business = All<BusinessComponent>().FirstOrDefault(b => b.Parent.Id == id);
                if (business == null) Missing(type, id); else Business(business);
                break;
            case "house":
                var house = All<HouseComponent>().FirstOrDefault(h => h.Parent.Id == id);
                if (house == null) Missing(type, id); else House(house);
                break;
            case "property":
                var property = All<PropertyComponent>().FirstOrDefault(p => p.Parent.Id == id);
                if (property == null) Missing(type, id); else Property(property);
                break;
            default: output.WriteLine("Unknown inspection type. " + Usage); break;
        }
    }

    private void Citizen(CitizenComponent citizen)
    {
        var entity = citizen.Parent;
        output.WriteLine($"Citizen {Person(citizen)} | entity {entity.Id} | {(citizen.Alive ? "alive" : "dead")} | age {citizen.Age}");
        Timestamp();
        output.WriteLine("Birthday: " + (citizen.Birthday.Day == 0 || citizen.Birthday.Year <= 0 ? "not set" :
            FormattableString.Invariant($"{citizen.Birthday.Year:0000}-{(int)citizen.Birthday.Month + 1:00}-{citizen.Birthday.Day:00}")));
        var needs = store.GetComponent<NeedsComponent>(entity);
        output.WriteLine(needs == null ? "Needs: not initialized" :
            $"Needs: hunger {N(needs.Hunger * 100)}% | thirst {N(needs.Thirst * 100)}% | energy {N(needs.Energy * 100)}%");
        var account = store.GetComponent<BankAccountComponent>(entity);
        var wallet = store.GetComponent<WalletComponent>(entity);
        output.WriteLine($"Bank: {(account == null ? "not initialized" : Money(account.Balance))} | " +
            $"cash: {(wallet == null ? "not initialized" : Money(wallet.Money))}");
        Inventory(store.GetComponent<InventoryComponent>(entity));

        var housing = Service<IHousingService>();
        var residence = housing?.GetResidence(citizen);
        if (housing == null) output.WriteLine("Residence: unavailable (housing service not supplied)");
        else if (residence == null) output.WriteLine("Residence: none");
        else
        {
            output.WriteLine($"Residence: house entity {residence.House.Parent.Id} | " +
                $"{(residence.Lease == null ? "owner occupied" : "rented")} | moved in {Time(residence.MovedInAt)}");
            if (residence.Lease is { } lease)
                output.WriteLine($"  Landlord: {Person(lease.Landlord)} | daily rent {Money(lease.DailyRent)} | arrears {Money(lease.Arrears)}");
            output.WriteLine($"  Access: sleep {Yes(housing.CanSleep(entity))} | drink {Yes(housing.CanDrink(entity))} | " +
                $"cook {Yes(housing.CanCook(entity))} | store {Yes(housing.CanStore(entity))}");
        }
        List("Owned properties (entity IDs)", All<PropertyComponent>().Where(p => ReferenceEquals(p.Owner, citizen))
            .Select(p => $"{p.Parent.Id}: {p.Type} | condition {N(p.Condition)}% | destroyed {Yes(p.Destroyed)}"));
        List("Owned businesses (entity IDs)", All<BusinessComponent>().Where(b => ReferenceEquals(b.Owner, citizen))
            .Select(b => $"{b.Parent.Id}: {b.Name} | {b.State} | issue {b.Issue}"));

        var businesses = Service<IBusinessService>();
        var employment = businesses?.GetEmployment(citizen);
        if (businesses == null) output.WriteLine("Employment: unavailable (business service not supplied)");
        else if (employment == null) output.WriteLine("Employment: none");
        else
        {
            output.WriteLine($"Employment: {employment.Business.Name} (entity {employment.Business.Parent.Id}) | {employment.Contract.Role}");
            Contract(employment.Contract, "  ");
            var shift = businesses.GetNextShift(citizen);
            output.WriteLine(shift == null ? "  Next shift: none" : $"  Next shift: {Time(shift.Start)} -> {Time(shift.End)}");
        }
        var profile = store.GetComponent<WorkerProfileComponent>(entity);
        if (profile != null)
            output.WriteLine($"Skills: {string.Join(", ", profile.Skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}={s.Value}"))} | commute {N(profile.CommuteMinutes)} min");
        Mind(entity);
    }

    private void Mind(EntityRef entity)
    {
        var mind = store.GetComponent<MindComponent>(entity);
        if (mind == null) { output.WriteLine("Mind: not initialized"); return; }
        output.WriteLine($"Mind: {(mind.IsActive ? "active" : "inactive")} | goal {mind.CurrentGoal?.Id ?? "none"}");
        var emergencies = mind.EmergencyActive.Where(e => e.Value).Select(e => e.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        output.WriteLine($"Emergencies: {(emergencies.Length == 0 ? "none" : string.Join(", ", emergencies))}");
        if (mind.Execution is not { } execution) output.WriteLine("Plan: none");
        else
        {
            output.WriteLine($"Plan: {execution.Status} | step {Math.Min(execution.StepIndex + 1, execution.Plan.PlanSteps.Count)}/{execution.Plan.PlanSteps.Count} | " +
                $"{(execution.Started ? "started" : "not started")} | elapsed {N(execution.Elapsed.TotalMinutes)} min | reservations {execution.Reservations.Count}");
            foreach (var (step, index) in execution.Plan.PlanSteps.Skip(execution.StepIndex).Take(5).Select((step, index) => (step, index)))
                output.WriteLine($"  {(index == 0 ? "Current" : "Next")}: {step.Candidate.Definition.Id} | estimate {N(step.DurationMinutes)} min | " +
                    string.Join(", ", step.Candidate.Bindings.OrderBy(b => b.Key, StringComparer.Ordinal).Select(b => $"{b.Key}={Value(b.Value)}")));
            if (execution.Plan.PlanSteps.Count - execution.StepIndex > 5) output.WriteLine("  ... more steps");
        }
        foreach (var retry in mind.GoalRetryTicks.Where(r => r.Value > clock.Tick).OrderBy(r => r.Key, StringComparer.Ordinal))
            output.WriteLine($"Retry: {retry.Key} at tick {retry.Value} ({retry.Value - clock.Tick} ticks remaining)");
        var minds = Service<ICitizenMindService>();
        if (minds != null)
        {
            var schedule = minds.GetSchedule(entity);
            List("Pending schedule (next 6)", schedule.Where(s => s.Status == CommitmentStatus.PENDING)
                .OrderBy(s => s.Commitment.Window.Start).Take(6).Select(Scheduled));
            List("Recent schedule results (last 3)", schedule.Where(s => s.Status != CommitmentStatus.PENDING)
                .OrderByDescending(s => s.Commitment.Window.End).Take(3).Select(Scheduled));
        }
        var diagnostics = systems?.GetSystems().OfType<CitizenMindSystem>().FirstOrDefault()?.GetPlanningDiagnostics();
        if (diagnostics != null)
            List("Recent planning results (last 3 retained)", diagnostics.RecentRequests.Where(r => r.Citizen == entity).TakeLast(3)
                .Select(r => $"{r.GoalId}: {r.Outcome} | {(r.IsValidation ? "validation" : "search")} | " +
                    $"queue {N(r.QueueWait.TotalMilliseconds)} ms | lifetime {N(r.Lifetime.TotalMilliseconds)} ms | work {r.WorkUnits}"));
    }

    private void Business(BusinessComponent business)
    {
        output.WriteLine($"Business entity {business.Parent.Id}: {business.Name} | {business.Type.Id}");
        Timestamp();
        output.WriteLine($"State: {business.State} | operating {Yes(business.IsOperating)} | issue {business.Issue} | production issue {business.ProductionIssue}");
        output.WriteLine($"Owner: {Person(business.Owner)} | premises entity {business.Property.Parent.Id}");
        output.WriteLine($"Premises: {business.Property.Type} | owner {Person(business.Property.Owner)} | area {N(business.Property.AreaM2)} m2 | " +
            $"condition {N(business.Property.Condition)}% | destroyed {Yes(business.Property.Destroyed)}");
        output.WriteLine($"Finances: balance {Money(business.PaymentAccount.Balance)} | revenue {Money(business.SalesRevenue)} | expenses {Money(business.Expenses)} | profit {Money(business.Profit)}");
        output.WriteLine($"Funding: startup {Money(business.StartupFunding)} (awarded {Yes(business.StartupFundingAwarded)}) | owner {Money(business.OwnerFunding)}");
        output.WriteLine($"Bills: paid {Money(business.OperatingCostsPaid)} | unpaid {Money(business.UnpaidOperatingCosts)} | wages paid {Money(business.WagesPaid)} | wages unpaid {Money(business.UnpaidWages)}");
        output.WriteLine($"Operating-cost recipient: {(business.OperatingCostRecipient == null ? "none" : $"account entity {business.OperatingCostRecipient.Parent.Id}")}");
        output.WriteLine($"Utilities: grid {Yes(business.GridConnected)} | electricity {N(business.ElectricityKwh)} kWh | water {N(business.WaterLitres)} litres");
        output.WriteLine($"Customer capacity: {N(business.AvailableCustomerCapacity)}");
        List("Opening hours", business.Type.OpeningHours.Select(h => $"{string.Join(", ", h.Days)} {h.Opens}-{h.Closes}"));
        List("Staffing", business.Type.Workforce.Select(role =>
            $"{role.Role}: {business.Employees.Count(e => e.IsEmployed && e.Role == role.Role)}/{role.DesiredCount} | minimum skill {role.MinimumSkill} | starting wage {Money(role.StartingHourlyWage)}/hour"));
        output.WriteLine("Employees:");
        if (business.Employees.Count == 0) output.WriteLine("  none");
        foreach (var employee in business.Employees.OrderBy(e => e.Employee.Id))
        {
            output.WriteLine($"  {Person(employee.Employee)} | {employee.Role} | employed {Yes(employee.IsEmployed)}");
            Contract(employee, "    ");
        }
        if (business.ActiveBatch is not { } batch) output.WriteLine("Production batch: none");
        else
        {
            output.WriteLine($"Production batch: {batch.Recipe.Id} | ready {Yes(batch.IsReadyToComplete)} | " +
                $"time {N(batch.ElapsedMinutes)}/{N(batch.Recipe.DurationMinutes)} min | labor {N(batch.LaborMinutesWorked)}/{N(batch.Recipe.LaborMinutes)} min");
            List("Reserved inputs", batch.ReservedInputs.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => $"{i.Key}: {N(i.Value)}"));
        }
        Inventory(business.Inventory, business.ActiveBatch?.ReservedEquipment.Keys.Concat(business.ActiveBatch.ReservedInputs.Keys));
    }

    private void House(HouseComponent house)
    {
        output.WriteLine($"House entity {house.Parent.Id}");
        Property(house.Property);
        output.WriteLine($"Occupancy: {house.Occupants.Count}/{house.MaxOccupants}");
        output.WriteLine($"Facilities: beds {Yes(house.HasBeds)} | safe water {Yes(house.HasSafeWater)} | kitchen {Yes(house.HasKitchen)} | storage {N(house.StorageCapacity)}");
        var utilities = Service<IHomeUtilityService>();
        output.WriteLine(utilities == null ? "Metered utilities: unavailable" :
            $"Metered utilities: water {Yes(utilities.HasWater(house.Parent))} | electricity {Yes(utilities.HasElectricity(house.Parent))}");
        List("Occupants", house.Occupants.OrderBy(c => c.Id).Select(Person));
    }

    private void Property(PropertyComponent property)
    {
        output.WriteLine($"Property entity {property.Parent.Id}: {property.Type} | owner {Person(property.Owner)}");
        output.WriteLine($"Area: {N(property.AreaM2)} m2 | capacity {property.Capacity} | base value {N(property.BaseValue)} | condition {N(property.Condition)}% | destroyed {Yes(property.Destroyed)}");
        var housing = Service<IHousingService>();
        if (housing == null) { output.WriteLine("Listings: unavailable"); return; }
        var sale = housing.GetSaleListings().FirstOrDefault(s => ReferenceEquals(s.Property, property));
        var rental = housing.GetRentalListings().FirstOrDefault(r => ReferenceEquals(r.House.Property, property));
        output.WriteLine(sale == null ? "Sale: not listed" : $"Sale: {Money(sale.Price)} | seller {Person(sale.Seller)} | listing {sale.Id}");
        output.WriteLine(rental == null ? "Rental: not listed" : $"Rental: {Money(rental.DailyRent)}/day | landlord {Person(rental.Landlord)} | listing {rental.Id}");
    }

    private void Contract(EmploymentContract contract, string indent)
    {
        output.WriteLine($"{indent}Wage {Money(contract.HourlyWage)}/hour | at workplace {Yes(contract.IsAtWorkplace)} | checked in {Yes(contract.IsCheckedIn)} | worked {N(contract.MinutesWorked)} min");
        output.WriteLine($"{indent}Wages: accrued {Money(contract.WagesAccrued)} | paid {Money(contract.WagesPaid)} | unpaid {Money(contract.UnpaidWages)}");
        output.WriteLine($"{indent}Last completed shift: {(contract.LastCompletedShiftEnd == TimeSpan.Zero ? "none" : Time(contract.LastCompletedShiftEnd))}" +
            (contract.IsCheckedIn ? $" | checked in {Time(contract.CheckedInAt)} -> {Time(contract.CheckedInUntil)}" : ""));
    }

    private void Inventory(InventoryComponent? inventory, IEnumerable<string>? reservedItems = null)
    {
        if (inventory == null) { output.WriteLine("Inventory: not initialized"); return; }
        output.WriteLine($"Inventory: used {N(inventory.UsedCapacity)} / {(inventory.Capacity is decimal capacity ? N(capacity) : "unlimited")} | " +
            $"free {(inventory.Capacity == null ? "unlimited" : N(inventory.FreeCapacity))} | total value {Money(inventory.TotalValue)}");
        List("Items (available / reserved / available value)", inventory.Inventory.Keys.Concat(reservedItems ?? [])
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
            .Where(id => inventory.GetQuantity(id) != 0 || inventory.GetReservedQuantity(id) != 0)
            .Select(id => $"{id}: {N(inventory.GetQuantity(id))} / {N(inventory.GetReservedQuantity(id))} / {Money(inventory.GetValue(id))}"));
    }

    private IEnumerable<T> All<T>() where T : class, IComponent => store.Query(ComponentTypes.Get<T>())
        .OrderBy(entity => entity.Id).Select(entity => store.GetComponent<T>(entity)!);
    private T? Service<T>() where T : class => services != null && services.TryGet<T>(out var service) ? service : null;
    private void Missing(string type, uint id) => output.WriteLine($"Could not find {type} with {(type == "citizen" ? "citizen" : "entity")} ID {id}.");
    private void List(string title, IEnumerable<string> entries)
    {
        output.WriteLine(title + ":");
        bool any = false;
        foreach (string entry in entries) { output.WriteLine("  " + entry); any = true; }
        if (!any) output.WriteLine("  none");
    }
    private void Timestamp()
    {
        var calendar = Service<ICalenderService>();
        output.WriteLine(calendar == null ? $"Tick {clock.Tick}" :
            $"Tick {clock.Tick} | game {calendar.GetDate(calendar.GetGameTime(clock.Tick)):yyyy-MM-dd} {Time(calendar.GetGameTime(clock.Tick))}");
    }
    private static string Scheduled(ScheduledCommitmentState state) =>
        $"{state.Status}: {state.Commitment.GoalId} | {Time(state.Commitment.Window.Start)} -> {Time(state.Commitment.Window.End)} | " +
        $"priority {state.Commitment.Priority} | source {state.SourceId ?? state.SourceRuleId ?? "manual"}" +
        (state.Commitment.Target is { } target ? $" | target {target.Fact} >= {N(target.Minimum)}" : "");
    private static string Person(CitizenComponent? citizen) => citizen == null ? "none" : $"#{citizen.Id} {citizen.Name}";
    private static string Yes(bool value) => value ? "yes" : "no";
    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string N(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Time(TimeSpan value) => FormattableString.Invariant($"day {value.Days} {value.Hours:00}:{value.Minutes:00}");
    private static string Value(object? value) => value switch
    {
        EntityRef entity => $"entity {entity.Id}",
        IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "null"
    };
}
