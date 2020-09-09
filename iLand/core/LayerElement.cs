namespace iLand.core
{
    internal class LayerElement
    {
        public string name;
        public string description;
        public GridViewType view_type;

        public LayerElement() { }
        public LayerElement(string aname, string adesc, GridViewType type)
        {
            name = aname;
            description = adesc;
            view_type = type;
        }
    }
}
