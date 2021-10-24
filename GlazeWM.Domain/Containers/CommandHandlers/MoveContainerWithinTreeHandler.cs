﻿using System.Linq;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Utils;

namespace GlazeWM.Domain.Containers.CommandHandlers
{
  class MoveContainerWithinTreeHandler : ICommandHandler<MoveContainerWithinTreeCommand>
  {
    private Bus _bus;
    private ContainerService _containerService;

    public MoveContainerWithinTreeHandler(Bus bus, ContainerService containerService)
    {
      _bus = bus;
      _containerService = containerService;
    }

    public CommandResponse Handle(MoveContainerWithinTreeCommand command)
    {
      var container = command.Container;
      var targetParent = command.TargetParent;
      var targetIndex = command.TargetIndex;

      // TODO: Handle case where target parent doesn't have children. Not sure if this is an issue anymore.

      // Get lowest common ancestor (LCA) between `container` and `targetParent`. This could be the
      // `targetParent` itself.
      var lowestCommonAncestor = _containerService.GetLowestCommonAncestor(container, targetParent);

      if (targetParent == lowestCommonAncestor)
      {
        MoveToLowestCommonAncestor(container, lowestCommonAncestor, targetIndex);
        return CommandResponse.Ok;
      }

      // Get ancestors of `container` and `targetParent` that are direct children of the LCA. This
      // could be the `container` or `targetParent` itself.
      var containerAncestor = container.SelfAndAncestors
        .First(ancestor => ancestor.Parent == lowestCommonAncestor);

      // Get whether the container is the focused descendant in its original subtree.
      var isFocusedDescendant = container == containerAncestor
        ? true : containerAncestor.LastFocusedDescendant == container;

      var targetParentAncestor = targetParent.SelfAndAncestors
        .First(ancestor => ancestor.Parent == lowestCommonAncestor);

      // Get whether the ancestor of `container` appears before `targetParent`'s ancestor in the
      // `ChildFocusOrder` of LCA.
      var originalFocusIndex = containerAncestor.FocusIndex;
      var isSubtreeFocused = originalFocusIndex < targetParentAncestor.FocusIndex;

      _bus.Invoke(new DetachContainerCommand(container));

      _bus.Invoke(new AttachContainerCommand(targetParent as SplitContainer, container, targetIndex));

      // Set `container` as focused descendant within target subtree if its original subtree had
      // focus more recently (even if the container is not the last focused within that subtree).
      if (isSubtreeFocused)
        _bus.Invoke(new SetFocusedDescendantCommand(container, targetParentAncestor));

      // If the focused descendant is moved to the targets subtree, then the target's ancestor
      // should be placed before the original ancestor in LCA's `ChildFocusOrder`.
      if (isFocusedDescendant && isSubtreeFocused)
        lowestCommonAncestor.ChildFocusOrder.ShiftToIndex(
          originalFocusIndex,
          targetParentAncestor
        );

      return CommandResponse.Ok;
    }

    private void MoveToLowestCommonAncestor(Container container, Container lowestCommonAncestor, int targetIndex)
    {
      // Keep reference to focus index of container's ancestor in LCA's `ChildFocusOrder`.
      var originalFocusIndex = container.SelfAndAncestors
        .First(ancestor => ancestor.Parent == lowestCommonAncestor)
        .FocusIndex;

      // Keep reference to container index and number of children that LCA has.
      var originalIndex = container.Index;
      var originalLcaChildCount = lowestCommonAncestor.Children.Count;

      _bus.Invoke(new DetachContainerCommand(container));

      var newLcaChildCount = lowestCommonAncestor.Children.Count;
      var shouldAdjustTargetIndex = originalLcaChildCount > newLcaChildCount
        && originalIndex < targetIndex;

      // Adjust for when target index changes on detach of container. For example, when shifting a
      // top-level container to the right in a workspace.
      var adjustedTargetIndex = shouldAdjustTargetIndex ? targetIndex - 1 : targetIndex;

      _bus.Invoke(new AttachContainerCommand(lowestCommonAncestor as SplitContainer, container, adjustedTargetIndex));

      lowestCommonAncestor.ChildFocusOrder.ShiftToIndex(originalFocusIndex, container);
    }
  }
}
