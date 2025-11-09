using System.Collections.Generic;

namespace Atomic.Elements
{
    public interface IReactiveArray<T> : IReadOnlyList<T>
    {
        event ChangeItemHandler<T> OnItemChanged;
        event StateChangedHandler OnStateChanged;
        
        int IReadOnlyCollection<T>.Count => this.Length;
        int Length { get; }
        
        T IReadOnlyList<T>.this[int index] => this[index];
        new T this[int index] { get; set; }
    }
}