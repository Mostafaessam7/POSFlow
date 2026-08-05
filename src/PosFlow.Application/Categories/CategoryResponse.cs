namespace PosFlow.Application.Categories;

public sealed record CategoryResponse(
    Guid Id,
    string NameAr,
    string? NameEn
);
