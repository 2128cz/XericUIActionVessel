namespace XericUI.Form
{
    public interface IActivity
    {
        public void Show();
        public void Hide();
    }

    public interface IVisibility : IActivity
    {
        public void Invisibility();
        public void Visibility();
    }

    public static class ActivedExtend
    {
        public static bool Show(this IActivity target, bool isShow)
        {
            if (target == null) return isShow;
            if (isShow)
                target.Show();
            else
                target.Hide();
            return isShow;
        }

        public static bool Visibility(this IVisibility target, bool isVisibility)
        {
            if (target == null) return isVisibility;
            if (isVisibility)
                target.Visibility();
            else
                target.Invisibility();
            return isVisibility;
        }
    }
}