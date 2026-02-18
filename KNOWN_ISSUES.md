# Known Issues - v3.0 Professional Branch

⚠️ **CRITICAL**: All v3.0 modules are **DISABLED BY DEFAULT** due to game-breaking bugs.

## Game-Breaking Issues (v3.0 modules)

### 1. NetworkBatchingModule ❌
**Status**: Experimental - Disabled by default  
**Problem**: Cannot serialize Quaternion rotations
```
[Warning: Unity Log] [NetworkBatching] Unsupported parameter type: Quaternion
```
**Impact**: 
- Objects cannot be placed (rotation missing)
- Object transforms fail to replicate
- Any RPC with Quaternion parameter breaks

**Fix needed**: Add Quaternion support to SerializeBatch() method

**Workaround**: Keep disabled until fixed

---

### 2. SaveSystemV2Module ❌
**Status**: Experimental - Disabled by default  
**Problem**: Cannot find game's SaveManager methods
```
AccessTools.Method: Could not find method for type Game.Saving.SaveManager and name SaveGame
```
**Impact**:
- Infinite save loop (original save + v2 save conflict)
- Save file corruption risk
- Game freezes on save

**Fix needed**: Find correct SaveManager method signatures via reflection

**Workaround**: Use original v2.0 save system (keep module disabled)

---

### 3. ObjectPoolModule ❌
**Status**: Experimental - Disabled by default  
**Problem**: Intercepts all Instantiate/Destroy calls globally
**Impact**:
- Objects spawn then disappear immediately
- Pool returns wrong object types
- Conflicts with game's own pooling

**Fix needed**: Whitelist approach - only pool specific prefab names

**Workaround**: Keep disabled

---

### 4. PathfindingCacheModule ⚠️
**Status**: Infrastructure ready - Disabled by default  
**Problem**: Not integrated with game's pathfinding system
```
Note: Actual pathfinding patches depend on game's specific implementation
```
**Impact**: Module loads but does nothing (no patches applied)

**Fix needed**: Find and patch game's actual pathfinding classes

**Workaround**: Keep disabled (no harm, just useless)

---

### 5. LagCompensationModule ⚠️
**Status**: Experimental - Disabled by default  
**Problem**: Not fully tested, may interfere with object placement
**Impact**: Unknown - needs testing

**Workaround**: Keep disabled until thoroughly tested

---

## Working Features (v2.0 modules)

### ✅ CurvyOptimization
**Status**: Production ready  
**Results**: 0 Curvy exceptions (was 30,000+), -30% CPU

### ✅ ReplicationWatchdog  
**Status**: Production ready  
**Results**: Client loading 10-30s (was infinite or 120s timeout)

### ✅ RpcSync
**Status**: Production ready  
**Results**: 0 RPC crashes (auto-resync on desync)

### ✅ DateTimeFix
**Status**: Production ready  
**Results**: All DateTime bugs fixed at source

### ✅ UIStability
**Status**: Production ready  
**Results**: 0 lobby/menu crashes from NullRef

---

## Recommendations

### For Players:
**Keep default config** - All v2.0 fixes enabled, all v3.0 optimizations disabled

Config file location:
```
BepInEx/config/com.mechanica.multiplayerfix.v2.cfg
```

### For Developers:
To enable v3.0 modules for testing, edit config:
```ini
[V3_Optimizations]
NetworkBatching = true  # WARNING: Breaks Quaternion
SaveSystemV2 = true     # WARNING: Infinite save loop
ObjectPool = true       # WARNING: Objects disappear
```

---

## Priority Fixes Needed

1. **NetworkBatching Quaternion support** (CRITICAL)
   - Add to SerializeBatch(): case for Quaternion (4 floats)
   - Test with object placement RPCs

2. **SaveSystemV2 method discovery** (CRITICAL)
   - Use AccessTools.GetDeclaredMethods() to find actual signatures
   - May need to patch different overloads

3. **ObjectPool whitelist** (HIGH)
   - Only pool prefabs with specific names
   - Don't intercept all Instantiate calls

4. **PathfindingCache integration** (MEDIUM)
   - Find game's pathfinding classes (likely in Game.Navigation)
   - Patch actual pathfinding methods

5. **LagCompensation testing** (LOW)
   - Test with 200ms+ latency
   - Verify no object placement interference

---

## Architecture Notes

### V2.0 (Bug Fixes) - Priority 0-199
Stable, production-ready patches for critical bugs

### V3.0 (Optimizations) - Priority 100-399
Experimental performance improvements - not production ready

### Module Priority System:
```
0-99:   Core/Utils (DateTimeFix)
100-199: Network (ReplicationWatchdog, RpcSync, NetworkBatching, LagComp)
200-299: Saving (SaveSystemV2)
300-399: Performance (CurvyOpt, PathfindingCache, ObjectPool)
400+:    UI (UIStability)
```

---

## Testing Instructions

### To test NetworkBatching fix:
1. Enable in config
2. Join multiplayer game
3. Try placing objects
4. Check logs for Quaternion warnings
5. If objects place correctly → fixed!

### To test SaveSystemV2 fix:
1. Enable in config
2. Save game
3. Check logs for method not found warnings
4. Verify save completes (not infinite)
5. Try loading save
6. If load works → fixed!

---

**Last Updated**: 2026-02-18  
**Branch**: v3.0-professional  
**Safe Version**: v2.0 modules only (current default)
