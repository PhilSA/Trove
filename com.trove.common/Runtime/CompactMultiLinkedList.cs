// using System.Runtime.CompilerServices;
// using Unity.Collections;
// using Unity.Collections.LowLevel.Unsafe;
// using Unity.Entities;
//
// namespace Trove
// {
//     public interface ICompactMultiLinkedListElement
//     {
//         public int PrevElementIndex { get; set; }
//     }
//
//     /// <summary>
//     /// Represents a sub-list of linked elements stored as part of a single larger list. The larger list is compact.
//     /// </summary>
//     public struct CompactMultiLinkedList
//     {
//         public int LastElementIndex;
//
//         public bool HasAnyElements => LastElementIndex >= 0;
//         
//         public static CompactMultiLinkedList Create()
//         {
//             return new CompactMultiLinkedList
//             {
//                 LastElementIndex = -1,
//             };
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static void Add<T>(ref CompactMultiLinkedList cmlList, ref DynamicBuffer<T> multiLinkedListBuffer, T addedElement)
//             where T : unmanaged, ICompactMultiLinkedListElement
//         {
//             // Add element at the end of the buffer, and remember the previous element index
//             int addIndex = multiLinkedListBuffer.Length;
//             addedElement.PrevElementIndex = cmlList.LastElementIndex;
//             multiLinkedListBuffer.Add(addedElement);
//
//             // Update the last element index
//             cmlList.LastElementIndex = addIndex;
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static void Add<T>(ref CompactMultiLinkedList cmlList, ref NativeList<T> multiLinkedListBuffer, T addedElement)
//             where T : unmanaged, ICompactMultiLinkedListElement
//         {
//             // Add element at the end of the buffer, and remember the previous element index
//             int addIndex = multiLinkedListBuffer.Length;
//             addedElement.PrevElementIndex = cmlList.LastElementIndex;
//             multiLinkedListBuffer.Add(addedElement);
//
//             // Update the last element index
//             cmlList.LastElementIndex = addIndex;
//         }
//
//         public static Iterator<T> GetIterator<T>(CompactMultiLinkedList cmlList) 
//             where T : unmanaged, ICompactMultiLinkedListElement
//         {
//             return new Iterator<T>
//             {
//                 _iteratedElementIndex = cmlList.LastElementIndex,
//                 _prevIteratedElementIndex = -1,
//             };
//         }
//         
//         /// <summary>
//         /// Iterates a specified linked list in a dynamic buffer containing multiple linked lists.
//         /// Also allows removing elements during iteration.
//         public struct Iterator<T> where T : unmanaged, ICompactMultiLinkedListElement
//         {
//             internal int _iteratedElementIndex;
//             internal int _prevIteratedElementIndex;
//
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public bool GetNext(in DynamicBuffer<T> multiLinkedListsBuffer, out T element, out int elementIndex)
//             {
//                 if (_iteratedElementIndex >= 0)
//                 {
//                     element = multiLinkedListsBuffer[_iteratedElementIndex];
//                     ;
//                     elementIndex = _iteratedElementIndex;
//
//                     // Move to next index but remember previous (used for removing)
//                     _prevIteratedElementIndex = _iteratedElementIndex;
//                     _iteratedElementIndex = element.PrevElementIndex;
//
//                     return true;
//                 }
//
//                 element = default;
//                 elementIndex = -1;
//                 return false;
//             }
//
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public bool GetNext(in NativeList<T> multiLinkedListsBuffer, out T element, out int elementIndex)
//             {
//                 if (_iteratedElementIndex >= 0)
//                 {
//                     element = multiLinkedListsBuffer[_iteratedElementIndex];
//                     ;
//                     elementIndex = _iteratedElementIndex;
//
//                     // Move to next index but remember previous (used for removing)
//                     _prevIteratedElementIndex = _iteratedElementIndex;
//                     _iteratedElementIndex = element.PrevElementIndex;
//
//                     return true;
//                 }
//
//                 element = default;
//                 elementIndex = -1;
//                 return false;
//             }
//
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public unsafe ref T GetNextByRef(ref DynamicBuffer<T> multiLinkedListsBuffer, out bool success,
//                 out int elementIndex)
//             {
//                 if (_iteratedElementIndex >= 0)
//                 {
//                     ref T elementRef = ref UnsafeUtility.ArrayElementAsRef<T>(multiLinkedListsBuffer.GetUnsafePtr(),
//                         _iteratedElementIndex);
//                     elementIndex = _iteratedElementIndex;
//
//                     // Move to next index but remember previous (used for removing)
//                     _prevIteratedElementIndex = _iteratedElementIndex;
//                     _iteratedElementIndex = elementRef.PrevElementIndex;
//
//                     success = true;
//                     return ref elementRef;
//                 }
//
//                 success = false;
//                 elementIndex = -1;
//                 return ref UnsafeUtility.AsRef<T>(multiLinkedListsBuffer.GetUnsafePtr());
//             }
//
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public unsafe ref T GetNextByRef(ref NativeList<T> multiLinkedListsBuffer, out bool success,
//                 out int elementIndex)
//             {
//                 if (_iteratedElementIndex >= 0)
//                 {
//                     ref T elementRef = ref UnsafeUtility.ArrayElementAsRef<T>(multiLinkedListsBuffer.GetUnsafePtr(),
//                         _iteratedElementIndex);
//                     elementIndex = _iteratedElementIndex;
//
//                     // Move to next index but remember previous (used for removing)
//                     _prevIteratedElementIndex = _iteratedElementIndex;
//                     _iteratedElementIndex = elementRef.PrevElementIndex;
//
//                     success = true;
//                     return ref elementRef;
//                 }
//
//                 success = false;
//                 elementIndex = -1;
//                 return ref UnsafeUtility.AsRef<T>(multiLinkedListsBuffer.GetUnsafePtr());
//             }
//
//             /// <summary>
//             /// Note: will update the last indexes in the linkedListLastIndexes following removal.
//             /// Note: GetNext() must be called before this can be used.
//             /// </summary>
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public void RemoveCurrentIteratedElementAndUpdateIndexes(
//                 ref DynamicBuffer<T> multiLinkedListsBuffer,
//                 ref NativeList<CompactMultiLinkedList> allMultiLinkedLists,
//                 out int firstUpdatedLastIndexIndex)
//             {
//                 firstUpdatedLastIndexIndex = -1;
//                 int removedElementIndex = _prevIteratedElementIndex;
//
//                 if (removedElementIndex < 0)
//                 {
//                     return;
//                 }
//
//                 T removedElement = multiLinkedListsBuffer[removedElementIndex];
//
//                 // Remove element
//                 multiLinkedListsBuffer.RemoveAt(removedElementIndex);
//
//                 // Iterate all last list last indexes and update them 
//                 for (int i = 0; i < allMultiLinkedLists.Length; i++)
//                 {
//                     CompactMultiLinkedList iteratedList = allMultiLinkedLists[i];
//
//                     // If the iterated last index is greater than the removed index, decrement it
//                     if (iteratedList.LastElementIndex > removedElementIndex)
//                     {
//                         iteratedList.LastElementIndex -= 1;
//                         allMultiLinkedLists[i] = iteratedList;
//                         if (firstUpdatedLastIndexIndex < 0)
//                         {
//                             firstUpdatedLastIndexIndex = i;
//                         }
//                     }
//                     // If the iterated last index is the one we removed, update it with the prev index of the removed element
//                     else if (iteratedList.LastElementIndex == removedElementIndex)
//                     {
//                         iteratedList.LastElementIndex = removedElement.PrevElementIndex;
//                         allMultiLinkedLists[i] = iteratedList;
//                         if (firstUpdatedLastIndexIndex < 0)
//                         {
//                             firstUpdatedLastIndexIndex = i;
//                         }
//                     }
//                 }
//
//                 // Iterate all buffer elements starting from the removed index to update their prev indexes
//                 for (int i = removedElementIndex; i < multiLinkedListsBuffer.Length; i++)
//                 {
//                     T iteratedElement = multiLinkedListsBuffer[i];
//
//                     // If the prev index of this element is greater than the removed one, decrement it
//                     if (iteratedElement.PrevElementIndex > removedElementIndex)
//                     {
//                         iteratedElement.PrevElementIndex -= 1;
//                         multiLinkedListsBuffer[i] = iteratedElement;
//                     }
//                     // If the prev index of this element was the removed one, change its prev index to the removed one's
//                     // prev index.
//                     else if (iteratedElement.PrevElementIndex == removedElementIndex)
//                     {
//                         iteratedElement.PrevElementIndex = removedElement.PrevElementIndex;
//                         multiLinkedListsBuffer[i] = iteratedElement;
//                     }
//                 }
//             }
//
//             /// <summary>
//             /// Note: will update the last indexes in the linkedListLastIndexes following removal.
//             /// Note: GetNext() must be called before this can be used.
//             /// </summary>
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public void RemoveCurrentIteratedElementAndUpdateIndexes(
//                 ref NativeList<T> multiLinkedListsBuffer,
//                 ref NativeList<CompactMultiLinkedList> allMultiLinkedLists,
//                 out int firstUpdatedLastIndexIndex)
//             {
//                 firstUpdatedLastIndexIndex = -1;
//                 int removedElementIndex = _prevIteratedElementIndex;
//
//                 if (removedElementIndex < 0)
//                 {
//                     return;
//                 }
//
//                 T removedElement = multiLinkedListsBuffer[removedElementIndex];
//
//                 // Remove element
//                 multiLinkedListsBuffer.RemoveAt(removedElementIndex);
//
//                 // Iterate all last list last indexes and update them 
//                 for (int i = 0; i < allMultiLinkedLists.Length; i++)
//                 {
//                     CompactMultiLinkedList iteratedList = allMultiLinkedLists[i];
//
//                     // If the iterated last index is greater than the removed index, decrement it
//                     if (iteratedList.LastElementIndex > removedElementIndex)
//                     {
//                         iteratedList.LastElementIndex -= 1;
//                         allMultiLinkedLists[i] = iteratedList;
//                         if (firstUpdatedLastIndexIndex < 0)
//                         {
//                             firstUpdatedLastIndexIndex = i;
//                         }
//                     }
//                     // If the iterated last index is the one we removed, update it with the prev index of the removed element
//                     else if (iteratedList.LastElementIndex == removedElementIndex)
//                     {
//                         iteratedList.LastElementIndex = removedElement.PrevElementIndex;
//                         allMultiLinkedLists[i] = iteratedList;
//                         if (firstUpdatedLastIndexIndex < 0)
//                         {
//                             firstUpdatedLastIndexIndex = i;
//                         }
//                     }
//                 }
//
//                 // Iterate all buffer elements starting from the removed index to update their prev indexes
//                 for (int i = removedElementIndex; i < multiLinkedListsBuffer.Length; i++)
//                 {
//                     T iteratedElement = multiLinkedListsBuffer[i];
//
//                     // If the prev index of this element is greater than the removed one, decrement it
//                     if (iteratedElement.PrevElementIndex > removedElementIndex)
//                     {
//                         iteratedElement.PrevElementIndex -= 1;
//                         multiLinkedListsBuffer[i] = iteratedElement;
//                     }
//                     // If the prev index of this element was the removed one, change its prev index to the removed one's
//                     // prev index.
//                     else if (iteratedElement.PrevElementIndex == removedElementIndex)
//                     {
//                         iteratedElement.PrevElementIndex = removedElement.PrevElementIndex;
//                         multiLinkedListsBuffer[i] = iteratedElement;
//                     }
//                 }
//             }
//         }
//     }
// }