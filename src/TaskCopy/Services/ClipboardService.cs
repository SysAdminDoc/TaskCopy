using System.Runtime.InteropServices;
using System.Windows;

namespace TaskCopy.Services;

public sealed class ClipboardService
{
    public bool TryCopy(string text)
    {
        if (text is null) return false;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (Exception)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
        return false;
    }
}
