namespace Game
{
    public static class InfernalNamecard
    {
        public static ulong CalculateYears(uint devil, uint archdevil, uint satan) =>
            (ulong)devil + 10UL * archdevil + 100UL * satan;

        public static bool IsForced(ulong serviceYears) => serviceYears >= 100UL;

        public static string Resolve(string equippedId, ulong serviceYears)
        {
            if (serviceYears >= 1000UL) return "satan-service";
            if (serviceYears >= 500UL) return "archdevil-service";
            if (serviceYears >= 100UL) return "devil-service";
            return equippedId;
        }
    }
}