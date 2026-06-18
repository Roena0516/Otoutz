/// <summary>
/// The player who has "entered" via RFID this app run. Static so it survives scene loads
/// (FirstLoading → Menu → InGame → Result). Cleared on app exit only.
/// </summary>
public static class PlayerSession
{
    public static bool IsEntered { get; private set; }
    public static string Uid { get; private set; }
    public static string Name { get; private set; }

    public static void Enter(string uid, string name)
    {
        Uid = uid;
        Name = name;
        IsEntered = !string.IsNullOrEmpty(uid);
    }

    public static void Clear()
    {
        Uid = null;
        Name = null;
        IsEntered = false;
    }
}
