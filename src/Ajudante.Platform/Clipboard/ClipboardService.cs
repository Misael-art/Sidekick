using System.Windows.Forms;

namespace Ajudante.Platform.Clipboard;

public static class ClipboardService
{
    public static string GetText()
    {
        return InvokeSta(() => System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : "");
    }

    public static void SetText(string text)
    {
        InvokeSta(() => System.Windows.Clipboard.SetText(text ?? string.Empty));
    }

    private static void InvokeSta(Action action)
    {
        Exception? captured = null;
        using var completed = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        completed.Wait();

        if (captured is not null)
            throw captured;
    }

    private static T InvokeSta<T>(Func<T> func)
    {
        T? result = default;
        InvokeSta(() => result = func());
        return result!;
    }
}
