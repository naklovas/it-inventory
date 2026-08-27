using BookRunner.Application.Dtos;

namespace BookRunner.Web.Models;

/// <summary>
/// Gorev kartinin gorunumu icin gerekli veri: gorevin kendisi ve kullanicinin
/// bu gorev uzerindeki etkin yetkileri.
///
/// Yetkiler ayri bayraklar olarak tasinir; cunku rol izninin yaninda
/// <b>runbook sahipligi</b> de yetki acabilir ve karti olusturan sayfa bunu
/// zaten hesaplamistir.
/// </summary>
/// <param name="Task">Gosterilecek gorev.</param>
/// <param name="CanEdit">Gorev duzenleme/siralama yetkisi.</param>
/// <param name="CanDelete">Gorev silme yetkisi (yonetici veya runbook sahibi).</param>
/// <param name="CanAssign">Atama ekleme/kaldirma yetkisi.</param>
/// <param name="CanExecute">Durum degistirme ve devretme yetkisi.</param>
/// <param name="CanComment">Yorum yazma yetkisi.</param>
/// <param name="CanRunScript">Goreve bagli CSX script'ini calistirma yetkisi.</param>
public sealed record TaskCardModel(
    RunbookTaskDto Task,
    bool CanEdit,
    bool CanDelete,
    bool CanAssign,
    bool CanExecute,
    bool CanComment,
    bool CanRunScript)
{
    /// <summary>Runbook detay sayfasinin yetkilerinden kart modeli uretir.</summary>
    public static TaskCardModel From(RunbookTaskDto task, RunbookDetailViewModel page) => new(
        task,
        CanEdit: page.CanEditThis,
        CanDelete: page.CanDeleteTaskThis,
        CanAssign: page.CanAssignThis,
        CanExecute: page.CanExecuteThis,
        CanComment: page.CanCommentThis,
        CanRunScript: page.CanRunScript);
}
