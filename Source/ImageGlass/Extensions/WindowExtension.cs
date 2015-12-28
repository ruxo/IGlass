using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageGlass.Extensions{
    static class WindowExtension{
        public static Task Schedule(this Form form, Action action){
            if (form.InvokeRequired)
                return Task.Factory.FromAsync(form.BeginInvoke(action), form.EndInvoke);
            else {
                action();
                return Task.FromResult(0);
            }
        }
    }
}