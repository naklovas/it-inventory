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
/// <param name="CanResetToNotStarted">
/// Gorevi "Baslamadi" durumuna sifirlama yetkisi (yalnizca yonetici rolu,
/// test modundaki etkin role gore - bkz. TaskService.ChangeStatusAsync).
/// </param>
/// <param name="RollbackActive">
/// Runbook'un geri donus plani aktive edildi mi. Aktive edilmeden geri
/// donus adimlarinin durum butonlari gosterilmez - tarihleri henuz
/// hesaplanmamistir (bkz. Runbook.IsRollbackActive).
/// </param>
/// <param name="ScenarioActive">
/// Bu gorev bir senaryo adimiysa (Task.ScenarioGroup dolu), o senaryo su an
/// aktive edilmis mi. Ana akis gorevleri (ScenarioGroup bos) icin her zaman
/// true'dur. Aktive edilmeden senaryo adimlarinin durum butonlari
/// gosterilmez - tarihleri henuz hesaplanmamistir (bkz.
/// Runbook.ActiveScenarioGroup).
/// </param>
public sealed record TaskCardModel(
    RunbookTaskDto Task,
    bool CanEdit,
    bool CanDelete,
    bool CanAssign,
    bool CanExecute,
    bool CanComment,
    bool CanRunScript,
    bool CanResetToNotStarted,
    bool RollbackActive,
    bool ScenarioActive)
{
    /// <summary>Runbook detay sayfasinin yetkilerinden kart modeli uretir.</summary>
    public static TaskCardModel From(RunbookTaskDto task, RunbookDetailViewModel page) => new(
        task,
        CanEdit: page.CanEditThis,
        CanDelete: page.CanDeleteTaskThis,
        CanAssign: page.CanAssignThis,
        CanExecute: page.CanExecuteThis,
        CanComment: page.CanCommentThis,
        CanRunScript: page.CanRunScript,
        CanResetToNotStarted: page.CanManageAdmin,
        RollbackActive: page.Runbook.IsRollbackActive,
        ScenarioActive: task.ScenarioGroup == null || task.ScenarioGroup == page.Runbook.ActiveScenarioGroup);
}
