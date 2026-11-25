using System.Collections.Generic;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Un tipo de objeto del inventario y cuántos hay. Algunos objetos (una carta) señalan un punto del mundo
    /// que se puede ver en el mapa: <see cref="HasMark"/>, <see cref="MarkX"/> y <see cref="MarkZ"/> (metros locales).
    /// </summary>
    public readonly struct InventoryEntry
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Count;
        public readonly bool HasMark;
        public readonly float MarkX;
        public readonly float MarkZ;

        public InventoryEntry(string id, string name, int count, bool hasMark = false, float markX = 0f, float markZ = 0f)
        {
            Id = id;
            Name = name;
            Count = count;
            HasMark = hasMark;
            MarkX = markX;
            MarkZ = markZ;
        }
    }

    /// <summary>Objetos que lleva el jugador. Empieza vacío; los objetos del mismo tipo se apilan.</summary>
    public sealed class Inventory
    {
        private readonly List<InventoryEntry> _entries = new List<InventoryEntry>();

        public IReadOnlyList<InventoryEntry> Entries => _entries;

        public bool IsEmpty => _entries.Count == 0;

        /// <summary>Número total de objetos, contando cada unidad de una pila.</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (InventoryEntry entry in _entries) total += entry.Count;
                return total;
            }
        }

        public int CountOf(string id)
        {
            foreach (InventoryEntry entry in _entries)
                if (entry.Id == id) return entry.Count;
            return 0;
        }

        /// <summary>
        /// Añade objetos. Con <paramref name="hasMark"/> el objeto señala un punto del mapa; si la pila ya existía
        /// sin marca, la toma de este; si ya tenía una, la conserva.
        /// </summary>
        public void Add(string id, string name, int count = 1, bool hasMark = false, float markX = 0f, float markZ = 0f)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                InventoryEntry e = _entries[i];
                if (e.Id != id) continue;
                bool keepOld = e.HasMark || !hasMark;
                _entries[i] = new InventoryEntry(id, e.Name, e.Count + count,
                    keepOld ? e.HasMark : true, keepOld ? e.MarkX : markX, keepOld ? e.MarkZ : markZ);
                return;
            }
            _entries.Add(new InventoryEntry(id, name, count, hasMark, markX, markZ));
        }

        /// <summary>Quita unidades de un objeto (lo entrega, lo gasta). Devuelve false si no hay tantas; entonces no quita nada.</summary>
        public bool Remove(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return false;
            for (int i = 0; i < _entries.Count; i++)
            {
                InventoryEntry e = _entries[i];
                if (e.Id != id) continue;
                if (e.Count < count) return false;
                if (e.Count == count) _entries.RemoveAt(i);
                else _entries[i] = new InventoryEntry(id, e.Name, e.Count - count, e.HasMark, e.MarkX, e.MarkZ);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
