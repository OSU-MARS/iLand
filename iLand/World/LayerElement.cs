namespace iLand.World
{
    internal class LayerElement
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public GridViewType ViewType { get; set; }

        public LayerElement() 
        {
        }

        public LayerElement(string name, string desc, GridViewType type)
        {
            this.Name = name;
            this.Description = desc;
            this.ViewType = type;
        }
    }
}
