namespace iLand.World
{
    internal class LayerElement
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public GridViewType ViewType { get; set; }

        public LayerElement(string name, string description, GridViewType type)
        {
            this.Name = name;
            this.Description = description;
            this.ViewType = type;
        }
    }
}
