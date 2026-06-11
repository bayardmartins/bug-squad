namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// Handler for resource items (wood, stone, etc.).
    /// Resources don't equip visually and have no actions.
    /// This handler exists for consistency in the system.
    /// </summary>
    public class ResourceHandler : BaseItemHandler
    {
        // Resources don't show in hand
        public override bool HasVisualModel => false;




    }
}
