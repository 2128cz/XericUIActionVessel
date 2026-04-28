using XericUI.Core.Interface;

namespace XericUI.Helper
{
    public static class UIVesselHelper
    {
        public static void CallVisible(this IUIVesselVisibalControl control, bool underVisible)
        {
            if (underVisible)
            {
                control.UnderVisible();
            }
            else
            {
                control.OutOfVisible();
            }
        }
    }
}