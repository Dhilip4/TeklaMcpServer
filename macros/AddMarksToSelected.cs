namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            akit.Callback("acmdCreateAppliedMarksSelected", "", "main_frame");
        }
    }
}
