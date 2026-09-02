using System.ComponentModel.DataAnnotations;
using BookRunner.Domain.Enums;

namespace BookRunner.Application.Dtos;

/// <summary>Runbook liste/filtreleme parametreleri.</summary>
public sealed record RunbookFilter
{
    /// <summary>Baslik, kod veya aciklama icinde aranir.</summary>
    public string? Search { get; init; }

    public RunbookStatus[]? Statuses { get; init; }

    /// <summary>true: sadece sablonlar, false: sadece calisan runbook'lar, null: hepsi.</summary>
    public bool? IsTemplate { get; init; }

    public string? TemplateCategory { get; init; }

    /// <summary>Birden fazla runbook'u kapsayan ust baslik (bkz. Runbook.SeyirName).</summary>
    public string? SeyirName { get; init; }

    public Guid? OwnerUserId { get; init; }

    /// <summary>Bu kullanicinin (veya gruplarinin) gorevi olan runbook'lar.</summary>
    public Guid? AssignedToUserId { get; init; }

    public Guid? AssignedToGroupId { get; init; }

    public string? Tag { get; init; }

    public string? ServiceManagerWorkItemId { get; init; }

    public DateTimeOffset? PlannedStartFrom { get; init; }

    public DateTimeOffset? PlannedStartTo { get; init; }

    /// <summary>Siralama alani: code | title | status | plannedStart | createdAt | updatedAt.</summary>
    public string SortBy { get; init; } = "updatedAt";

    public bool SortDescending { get; init; } = true;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 25;
}

/// <summary>Liste satirinda gosterilen ozet runbook bilgisi.</summary>
public sealed record RunbookListItemDto
{
    public Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public RunbookStatus Status { get; init; }
    public required string StatusText { get; init; }
    public bool IsTemplate { get; init; }
    public string? TemplateCategory { get; init; }
    public string? SeyirName { get; init; }
    public DateTimeOffset? PlannedStart { get; init; }
    public DateTimeOffset? PlannedEnd { get; init; }
    public PersonSummary? Owner { get; init; }
    public string? ServiceManagerWorkItemId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public int TaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public int CommentCount { get; init; }
    /// <summary>Tamamlanma yuzdesi (0-100).</summary>
    public int ProgressPercent => TaskCount == 0 ? 0 : (int)Math.Round(CompletedTaskCount * 100.0 / TaskCount);
    /// <summary>Runbook uzerinde calisan kisilerin rozetleri (en fazla birkac tane).</summary>
    public IReadOnlyList<PersonSummary> Participants { get; init; } = Array.Empty<PersonSummary>();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>Runbook detay ekraninin tum verisi.</summary>
public sealed record RunbookDetailDto
{
    public Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public RunbookStatus Status { get; init; }
    public required string StatusText { get; init; }
    public bool IsTemplate { get; init; }
    public string? TemplateCategory { get; init; }
    public string? SeyirName { get; init; }
    public Guid? SourceTemplateId { get; init; }
    public string? SourceTemplateTitle { get; init; }
    public DateTimeOffset? PlannedStart { get; init; }
    public DateTimeOffset? PlannedEnd { get; init; }
    public DateTimeOffset? ActualStart { get; init; }
    public DateTimeOffset? ActualEnd { get; init; }
    public PersonSummary? Owner { get; init; }
    public string? ServiceManagerWorkItemId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RunbookTaskDto> Tasks { get; init; } = Array.Empty<RunbookTaskDto>();
    /// <summary>Sahibin bu runbook'a ozel olarak "Editor" yetkisi verdigi kisiler.</summary>
    public IReadOnlyList<RunbookCollaboratorDto> Collaborators { get; init; } = Array.Empty<RunbookCollaboratorDto>();
    public DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    /// <summary>Iyimser eszamanlilik damgasi (Base64 rowversion).</summary>
    public string? RowVersion { get; init; }
}

/// <summary>
/// Bir runbook'a sahibi tarafindan "Editor" olarak eklenmis kisi. Editor,
/// global rolu ne olursa olsun bu runbook'ta gorev ekleyip duzenleyebilir,
/// atama yapabilir ve yorum yazabilir; runbook'u silemez/duzenleyemez.
/// </summary>
public sealed record RunbookCollaboratorDto
{
    public Guid Id { get; init; }
    public required PersonSummary Person { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public required string AddedBy { get; init; }
}

/// <summary>Runbook'a editor ekleme istegi.</summary>
public sealed record AddRunbookCollaboratorRequest
{
    public Guid UserId { get; init; }
}

/// <summary>Runbook olusturma istegi.</summary>
public sealed record CreateRunbookRequest
{
    [Required, StringLength(250, MinimumLength = 3)]
    public required string Title { get; init; }

    [StringLength(8000)]
    public string? Description { get; init; }

    public bool IsTemplate { get; init; }

    [StringLength(100)]
    public string? TemplateCategory { get; init; }

    [StringLength(150)]
    public string? SeyirName { get; init; }

    public DateTimeOffset? PlannedStart { get; init; }

    public DateTimeOffset? PlannedEnd { get; init; }

    /// <summary>Bos birakilirsa runbook'u olusturan kullanici sahip olur.</summary>
    public Guid? OwnerUserId { get; init; }

    [StringLength(64)]
    public string? ServiceManagerWorkItemId { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>Runbook guncelleme istegi.</summary>
public sealed record UpdateRunbookRequest
{
    [Required, StringLength(250, MinimumLength = 3)]
    public required string Title { get; init; }

    [StringLength(8000)]
    public string? Description { get; init; }

    public RunbookStatus Status { get; init; }

    [StringLength(100)]
    public string? TemplateCategory { get; init; }

    [StringLength(150)]
    public string? SeyirName { get; init; }

    public DateTimeOffset? PlannedStart { get; init; }

    public DateTimeOffset? PlannedEnd { get; init; }

    public Guid? OwnerUserId { get; init; }

    [StringLength(64)]
    public string? ServiceManagerWorkItemId { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Ayni kaydi baskasi degistirdiyse istegi reddetmek icin gonderilir.</summary>
    public string? RowVersion { get; init; }
}

/// <summary>Mevcut runbook'u sablona cevirme / sablondan runbook uretme istegi.</summary>
public sealed record CreateFromTemplateRequest
{
    [Required, StringLength(250, MinimumLength = 3)]
    public required string Title { get; init; }

    public DateTimeOffset? PlannedStart { get; init; }

    public DateTimeOffset? PlannedEnd { get; init; }

    public Guid? OwnerUserId { get; init; }

    [StringLength(64)]
    public string? ServiceManagerWorkItemId { get; init; }

    /// <summary>true ise sablondaki atamalar da kopyalanir.</summary>
    public bool CopyAssignments { get; init; }
}
