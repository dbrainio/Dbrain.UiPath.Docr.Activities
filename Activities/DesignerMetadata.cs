using System.Activities.Presentation.Metadata;
using System.ComponentModel;


namespace Dbrain.UiPath.Docr.Activities.Design
{

    public class DesignerMetadata : IRegisterMetadata
    {

        public void Register()

        {

            AttributeTableBuilder attributeTableBuilder = new AttributeTableBuilder();
            attributeTableBuilder.AddCustomAttributes(typeof(Dbrain.UiPath.Docr.Activities.Docr), new DesignerAttribute(typeof(DocrDesigner)));
            MetadataStore.AddAttributeTable(attributeTableBuilder.CreateTable());

        }

    }

}
