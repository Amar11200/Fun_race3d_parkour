namespace DashMasters
{
    public enum GameState
    {
        StartMenu,
        LevelSelect,
        Countdown,
        Racing,
        Paused,
        Finished
    }

    public enum RacerRank
    {
        First = 1,
        Second = 2,
        Third = 3,
        Fourth = 4
    }

    [System.Serializable]
    public class CostumeDefinition
    {
        public int id;
        public string name;
        public int cost;
        public UnityEngine.Color bodyColor;
        public UnityEngine.Color accentColor;

        public CostumeDefinition(int id, string name, int cost, UnityEngine.Color body, UnityEngine.Color accent)
        {
            this.id = id;
            this.name = name;
            this.cost = cost;
            this.bodyColor = body;
            this.accentColor = accent;
        }
    }
}
