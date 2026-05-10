// ServerHistoryManager.cs (LINQ-Free Version)
using Godot;
using ImGuiNET;
using MonkeNet.Shared;
using System;
using System.Collections.Generic;

namespace MonkeNet.Server
{
    [GlobalClass]
    public partial class ServerHistoryManager : InternalServerComponent
    {
        [Export] public int MaxHistoryTicks { get; set; } = 60; // TODO: Magic number of 60 ticks saved

		private int _latestTick = 0;

        // Stores historical data: Tick -> EntityID -> State
        private readonly Dictionary<int, Dictionary<int, HistoricalState>> _historyBuffer = new();
		
        // Stores original transforms during a rewind scope
        private readonly Dictionary<int, Transform3D> _originalTransforms = new();

		private int _lastEntityCount = 0;
		private int _lastRewindLength = 0;

        public readonly struct RewindScope : IDisposable
        {
            private readonly ServerHistoryManager _manager;
            internal RewindScope(ServerHistoryManager manager) { /* ... */ _manager = manager; }
            public void Dispose() { _manager.RestoreCurrentState(); }
        }

        protected override void OnProcessTick(int currentTick)
        {
			_latestTick = currentTick;
            RecordState(currentTick);
            PruneHistory(currentTick);
        }

        private void RecordState(int tick)
        {
            if (!_historyBuffer.TryGetValue(tick, out var currentTickStates))
            {
                currentTickStates = new Dictionary<int, HistoricalState>();
                _historyBuffer[tick] = currentTickStates;
            }

            foreach (var entity in FindRewindableEntities())
            {
                if (entity is Node3D entityNode3D && entity is INetworkedEntity networkedEntity)
                {
                    currentTickStates[networkedEntity.EntityId] = new HistoricalState(entityNode3D.GlobalTransform);
                }
            }
			_lastEntityCount = currentTickStates.Count;
        }

        private void PruneHistory(int currentTick)
        {
            int cutoffTick = currentTick - MaxHistoryTicks;
            List<int> ticksToRemove = null; // Lazily initialized list

            foreach (int tick in _historyBuffer.Keys)
            {
                if (tick <= cutoffTick)
                {
                    // Allocate the list only if we find ticks to remove
                    if (ticksToRemove == null)
                    {
                        // You could give it an initial capacity if you expect frequent pruning
                        // e.g., new List<int>(10);
                        ticksToRemove = new List<int>();
                    }
                    ticksToRemove.Add(tick);
                }
            }

            // Remove the identified ticks
            if (ticksToRemove != null)
            {
                foreach (int tick in ticksToRemove)
                {
                    _historyBuffer.Remove(tick);
                }
            }
        }

        private HistoricalState? GetStateAtTick(int entityId, int tick)
        {
            if (_historyBuffer.TryGetValue(tick, out var tickStates))
            {
                if (tickStates.TryGetValue(entityId, out var state))
                {
                    return state;
                }
            }
            return null;
        }

        public RewindScope EnterRewindScope(int targetTick, int entityId = 0)
        {
            _originalTransforms.Clear();
			GD.Print($"Entering rewind scope for {targetTick} (latest tick {_latestTick})");

            foreach (var entity in FindRewindableEntities())
            {
                if (entity is Node3D entityNode3D && entity is INetworkedEntity networkedEntity)
                {
					// Clients see themselves as predicted, but other clients as interpolated
					// so only rewind other entities. To get the estimated perspective of 
					// the entity we were given.
					if (networkedEntity.EntityId == entityId) continue;
                    _originalTransforms[networkedEntity.EntityId] = entityNode3D.GlobalTransform;
                    HistoricalState? historicalState = GetStateAtTick(networkedEntity.EntityId, targetTick);
                    if (historicalState.HasValue)
                    {
                        entityNode3D.GlobalTransform = new Transform3D(historicalState.Value.Rotation, historicalState.Value.Position);
                    }
                    else
                    {
						GD.Print($"Could not rewind {networkedEntity.EntityId} to tick {targetTick}");
						_originalTransforms.Remove(networkedEntity.EntityId);
                    }
                }
            }
			_lastRewindLength = _latestTick - targetTick;
            return new RewindScope(this);
        }

        internal void RestoreCurrentState()
        {
            if (_originalTransforms.Count == 0) return;
            foreach (var kvp in _originalTransforms)
            {
                var entityNode = FindEntityNodeById(kvp.Key);
                if (entityNode is Node3D node3D)
                {
                    node3D.GlobalTransform = kvp.Value;
                }
            }
            _originalTransforms.Clear();
        }

        private IEnumerable<INetworkedEntity> FindRewindableEntities()
        {
            var spawner = MonkeNetConfig.Instance?.EntitySpawner;
             if (spawner?.Entities == null)
                 yield break;

            var entities = spawner.Entities;
            int count = entities.Count;
            for(int i = 0; i < count; ++i)
            {
                var entity = entities[i];
                if (entity is IServerSyncedEntity)
                {
                    yield return entity;
                }
            }
        }

        private Node FindEntityNodeById(int entityId)
        {
            var spawner = MonkeNetConfig.Instance?.EntitySpawner;
            return spawner?.GetNodeOrNull($"{entityId}");

            // Alternative loop-based approach (also LINQ-free) if names don't match IDs:
            /*
            if (MonkeNetConfig.Instance?.EntitySpawner?.Entities == null) return null;
            var entities = MonkeNetConfig.Instance.EntitySpawner.Entities;
            int count = entities.Count;
            for(int i = 0; i < count; ++i)
            {
                var entity = entities[i];
                if(entity.EntityId == entityId && entity is Node node)
                {
                    return node;
                }
            }
            return null;
            */
        }

		public void DisplayDebugInformation()
        {
            if (ImGui.CollapsingHeader("History Manager"))
            {
                ImGui.Text($"History Buffer {_historyBuffer.Count}");
				ImGui.Text($"Last rewind length: {_lastRewindLength}");
            }
        }
    }
}
