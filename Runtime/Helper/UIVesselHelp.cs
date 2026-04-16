using XericUI.Core.Interface;

namespace XericUI.Helper
{
    public static class UIVesselHelper
    {
        public static void CallVisibal(this IUIVesselVisibalControl control, bool underVisible)
        {
            if (underVisible)
            {
                control.UnderVisibal();
            }
            else
            {
                control.OutOfVisibal();
            }
        }
    }
}