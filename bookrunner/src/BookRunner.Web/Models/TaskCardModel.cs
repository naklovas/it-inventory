using BookRunner.Application.Dtos;

namespace BookRunner.Web.Models;

/// <summary>
/// Gorev kartinin gorunumu icin gerekli veri: gorevin kendisi ve kullanicinin
/// bu gorev uzerindeki yetkileri.
/// </summary>
/// <param name="Task">Gosterilecek gorev.</param>
/// <param name="Page">Yetki bilgilerini tasiyan sayfa modeli.</param>
public sealed record TaskCardModel(RunbookTaskDto Task, PageViewModel Page);
