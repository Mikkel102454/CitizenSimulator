namespace CitizenSimulator.Abstractions.ECS;

// hold reference to entity. before using it in a sense where it should be active always check if it is
public readonly record struct EntityRef(
    int Id,
    int Generation,
    int StoreId
);