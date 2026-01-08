using UnityEngine;

namespace Minigames.Base.Interaction
{
    /// <summary>
    /// Interface for objects that can receive dropped items.
    /// </summary>
    public interface IDropTarget
    {
        /// <summary>
        /// Checks if an item can be dropped at the given position.
        /// </summary>
        /// <param name="position">Screen position to check</param>
        /// <returns>True if drop is valid, false otherwise</returns>
        bool CanAcceptDrop(Vector2 position);

        /// <summary>
        /// Handles the drop of an item.
        /// </summary>
        /// <param name="draggable">The draggable item being dropped</param>
        /// <param name="position">Screen position of the drop</param>
        /// <returns>True if drop was successful, false otherwise</returns>
        bool OnDrop(IDraggable draggable, Vector2 position);

        /// <summary>
        /// Gets the transform of the drop target for positioning.
        /// </summary>
        RectTransform GetRectTransform();
    }

    /// <summary>
    /// Interface for draggable items.
    /// </summary>
    public interface IDraggable
    {
        /// <summary>
        /// Gets the transform of the draggable item.
        /// </summary>
        RectTransform GetRectTransform();

        /// <summary>
        /// Gets the index of the correct drop target for this item.
        /// </summary>
        int GetCorrectTargetIndex();
    }
}
