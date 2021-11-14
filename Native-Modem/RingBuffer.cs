#region License and Information
/*****
* This class implements a ring buffer of a fixed size. It's very similar to
* a generic Queue but it does not grow automatically but instead overwrites
* the oldest value. It also provides random access to all elements in the
* buffer. The index can be positive to access the elements from the read
* position or it can be negative to access the elements before the write
* position. So "0" is the oldest element, "-1" is the newest element. 
* 
* 
* 2017.06.02 - first version 
* 
* Copyright (c) 2017-2018 Markus Göbel (Bunny83)
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to
* deal in the Software without restriction, including without limitation the
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
* sell copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
* FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
* IN THE SOFTWARE.
* 
*****/
#endregion License and Information
using System.Collections;
using System.Collections.Generic;

namespace B83.Collections
{
    public class RingBuffer<T> : ICollection<T>, IList<T>
    {
        private T[] m_Data;
        private int m_Capacity = -1;
        private int m_Count;
        private int m_Read;
        private int m_Write;
        private int m_Version = 0;

        public int Count { get { return m_Count; } }
        public bool IsReadOnly { get { return false; } }
        public int Capacity
        {
            get { return m_Capacity; }
            set { SetCapacity(value); }
        }

        public T this[int index]
        {
            get
            {
                if (m_Count == 0)
                    throw new System.InvalidOperationException("RingBuffer.this[]:: buffer is empty");
                if (index < -m_Count || index >= m_Count)
                    throw new System.ArgumentOutOfRangeException("index", index, "RingBuffer.this[index] is out of range (-Count .. Count-1)");
                if (index >= 0)
                    index = (m_Read + index) % m_Capacity;
                else
                    index = (m_Capacity + m_Write + index) % m_Capacity;
                return m_Data[index];
            }
            set
            {
                if (m_Count == 0)
                    throw new System.InvalidOperationException("RingBuffer.this[]:: buffer is empty");
                if (index < -m_Count || index >= m_Count)
                    throw new System.ArgumentOutOfRangeException("index", index, "RingBuffer.this[index] is out of range (-Count .. Count-1)");
                if (index >= 0)
                    index = (m_Read + index) % m_Capacity;
                else
                    index = (m_Capacity + m_Write + index) % m_Capacity;
                m_Data[index] = value;
            }
        }

        public RingBuffer(int aSize)
        {
            SetCapacity(aSize);
            m_Count = 0;
            m_Read = 0;
            m_Write = 0;
        }

        void SetCapacity(int aSize)
        {
            aSize = System.Math.Max(aSize, 2);
            if (aSize == m_Capacity)
                return;
            var newData = new T[aSize];
            if (m_Data != null)
            {
                if (aSize > m_Capacity)
                {
                    CopyTo(newData, 0);
                    m_Read = 0;
                    m_Write = m_Count;
                }
                else if (aSize < m_Capacity)
                {
                    int dif = m_Count - aSize;
                    if (dif > 0)
                    {
                        m_Read = (m_Read + dif) % m_Capacity;
                        m_Count = aSize;
                    }
                    CopyTo(newData, 0);
                    m_Read = 0;
                    m_Write = m_Count % aSize;
                }
            }
            m_Data = newData;
            m_Capacity = aSize;
        }

        public T ReadAndRemoveNext()
        {
            if (m_Count == 0)
                throw new System.InvalidOperationException("Read not possible, RingBuffer is empty");
            T res = m_Data[m_Read];
            m_Data[m_Read] = default(T);
            m_Read = (m_Read + 1) % m_Capacity;
            m_Count--;
            m_Version++;
            return res;
        }
        public T ReadAndRemoveNext(T aDefaultValue)
        {
            if (m_Count == 0)
                return aDefaultValue;
            T res = m_Data[m_Read];
            m_Data[m_Read] = default(T);
            m_Read = (m_Read + 1) % m_Capacity;
            m_Count--;
            m_Version++;
            return res;
        }
        public T Peak(T aDefaultValue)
        {
            if (m_Count == 0)
                return aDefaultValue;
            return m_Data[m_Read];
        }

        public void Add(T aData)
        {
            m_Data[m_Write] = aData;
            m_Write = (m_Write + 1) % m_Capacity;
            if (m_Count == m_Data.Length)
                m_Read = (m_Read + 1) % m_Capacity;
            else
                m_Count++;
            m_Version++;
        }

        public void Clear()
        {
            for (int i = 0; i < m_Capacity; i++)
                m_Data[i] = default(T);
            m_Read = 0;
            m_Write = 0;
            m_Count = 0;
            m_Version++;
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < m_Capacity; i++)
                if (EqualityComparer<T>.Default.Equals(item, m_Data[i]))
                    return true;
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new System.ArgumentNullException("RingBuffer.CopyTo::Passed array is null");
            if (arrayIndex >= array.Length)
                throw new System.ArgumentOutOfRangeException("arrayIndex", "RingBuffer.CopyTo::Passed index is out of range");
            int space = array.Length - arrayIndex;
            if (space < m_Count)
                throw new System.ArgumentException("RingBuffer.CopyTo::Passed array is too small (" + space + " while " + m_Count + " is needed)");
            int a = System.Math.Min(m_Count, m_Capacity - m_Read);
            System.Array.Copy(m_Data, m_Read, array, arrayIndex, a);
            if (a < m_Count)
            {
                arrayIndex += a;
                System.Array.Copy(m_Data, 0, array, arrayIndex, m_Count - a);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            int version = m_Version;
            for (int i = 0; i < m_Count; i++)
            {
                int index = (m_Read + i) % m_Capacity;
                yield return m_Data[index];
                if (m_Version != version)
                    throw new System.InvalidOperationException("RingBuffer.IEumerator::The data has changed so the enumeration can't be continued");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < m_Count; i++)
            {
                int index = (m_Read + i) % m_Capacity;
                if (EqualityComparer<T>.Default.Equals(m_Data[index], item))
                    return i;
            }
            return -1;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= m_Count)
                throw new System.ArgumentOutOfRangeException("index", index, "RingBuffer.RemoveAt::index out of bounds");
            int toIndex = (m_Read + index) % m_Capacity;
            for (int i = index; i < m_Count; i++)
            {
                int fromIndex = (toIndex + 1) % m_Capacity;
                m_Data[toIndex] = m_Data[fromIndex];
                toIndex = fromIndex;
            }
            m_Data[toIndex] = default(T);
        }
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index == -1)
                return false;
            RemoveAt(index);
            return true;
        }

        public void Insert(int index, T item)
        {
            throw new System.NotSupportedException();
        }

        //Added by wjw78879
        public void FillWith(T item)
        {
            m_Count = m_Capacity;
            m_Write = 0;
            m_Read = 0;
            for (int i = 0; i < m_Capacity; i++)
            {
                m_Data[i] = item;
            }
        }
    }
}