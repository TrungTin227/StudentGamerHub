namespace Services.Common.Services;

public sealed record UpdateItem<TKey, TUpdate>(TKey Id, TUpdate Dto);



