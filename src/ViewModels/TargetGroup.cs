using System.Collections.Generic;

namespace Komorebi.ViewModels;

/// <summary>追加先グループの表示名と、保存対象のノードを分けて保持する。</summary>
public sealed record TargetGroup(RepositoryNode Node, string Name, string Description, bool IsAuto = false)
{
    public static void Collect(List<TargetGroup> output, List<RepositoryNode> nodes, string prefix = "")
    {
        foreach (var node in nodes)
        {
            if (node.IsRepository)
                continue;
            var name = string.IsNullOrEmpty(prefix) ? node.Name : $"{prefix}/{node.Name}";
            output.Add(new TargetGroup(node, name, string.Empty));
            Collect(output, node.SubNodes, name);
        }
    }
}
