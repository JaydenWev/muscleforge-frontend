using System.Globalization;
using MuscleForge.Mobile.Core.Measurements;
using MuscleForge.Mobile.Core.Templates;
using MuscleForge.Mobile.Views.Templates;

namespace MuscleForge.Mobile.Services;

public sealed class TemplatePages(ILocalTemplateStore store, ITemplateExercisePicker picker)
{
    public TemplateListPage CreateList(TemplateScope scope)
    {
        TemplateListPage? list = null;
        var unit = WeightUnit.Kg;
        Task Edit(Guid? id)
        {
            var editor = new TemplateEditorViewModel(store, scope, unit, CultureInfo.CurrentCulture, MainThread.BeginInvokeOnMainThread);
            return list!.Navigation.PushAsync(new TemplateEditorPage(editor, scope, picker, id, unit));
        }
        Task Open(Guid? id) => id.HasValue
            ? list!.Navigation.PushAsync(new TemplateDetailsPage(store, scope, id.Value, unit, value => Edit(value)))
            : Edit(null);
        list = new TemplateListPage(new TemplateListViewModel(store, scope, MainThread.BeginInvokeOnMainThread), Open);
        return list;
    }
}
