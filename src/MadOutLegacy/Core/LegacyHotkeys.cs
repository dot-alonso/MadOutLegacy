using UnityEngine;

internal static class LegacyHotkeys
{
    internal static string LastSelectedCarName;

    internal static void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.F5))
        {
            LegacyHelpers.SafeInvoke(typeof(WeatherManager), "DoRandom");
        }
        if (Input.GetKeyDown(KeyCode.F6) && !string.IsNullOrEmpty(LastSelectedCarName))
        {
            App_DeliveryCar.CreateDeliveryCar(LastSelectedCarName, false);
        }
        if (NetManager.isOnlineClient && Input.GetKeyDown(KeyCode.F12))
        {
            AskBoxUI.Show("Disconnect from server?", delegate(AskBoxUI box)
            {
                if (box.isYes && NetManager.me)
                {
                    NetManager.me.StopHost();
                }
            });
        }
    }
}
