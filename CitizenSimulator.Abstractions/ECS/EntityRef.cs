namespace CitizenSimulator.Abstractions.ECS;

public readonly record struct EntityRef(
    int Id,
    int Generation,
    int StoreId
);