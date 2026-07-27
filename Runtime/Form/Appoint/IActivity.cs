namespace XericUI.Form
{
    public interface IActivity
    {
        public void Show();
        public void Hide();
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
    }
}