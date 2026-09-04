using UnityEngine;

public class SceneDatabase : MonoBehaviour
{
    public class Slots
    {
        public const string Menu = "Menu";
        public const string Session = "Session";
        public const string SessionContent = "SessionContent";
    }

    public class Scenes
    {
        public const string Bootstrap = "bootstrap";
        public const string MainMenu = "main-menu";
        public const string Session = "session";
        
        public const string Intro = "intro";
        public const string Game = "game";
        public const string Credits = "credits";
        
    }
}
