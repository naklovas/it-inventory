namespace PromptBuilder.Models;

public class ProcessDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ProcessStep> Steps { get; set; } = [];
}
