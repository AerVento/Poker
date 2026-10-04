using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Content;
using UnityEngine;
using XLua;

namespace Game.UI.Gifts
{
    /// <summary>A gift module that loaded and passed validation: its metadata and build function.</summary>
    public sealed class GiftScript
    {
        internal LuaTable Module { get; }
        public float Duration { get; }
        public int ApiVersion { get; }
        public IReadOnlyList<string> Resources { get; }

        internal GiftScript(LuaTable module, float duration, int apiVersion, IReadOnlyList<string> resources)
        {
            Module = module;
            Duration = duration;
            ApiVersion = apiVersion;
            Resources = resources;
        }
    }

    /// <summary>
    /// The client's single Lua state for gift scripts. Scripts run in a sandbox without reflection, I/O, loading,
    /// error catching, coroutines or finalizers, under an instruction budget enforced by a count hook.
    /// </summary>
    [ContentScriptApi]
    public static class GiftScriptRuntime
    {
        public const int InstructionBudget = 2000000;

        // Trusted host code. The facade passed to build() wraps the C# host in closures, so a script only ever
        // holds Lua functions and numbers. Facade names and arguments are the script API (content format).
        public const string Bootstrap = @"
local sethook, load, pack, unpack = debug.sethook, load, table.pack, table.unpack
local error, type, pairs, ipairs, setmetatable, rawget = error, type, pairs, ipairs, setmetatable, rawget
local BUDGET = ...

local function guarded(fn, ...)
  sethook(function() error('gift script exceeded its instruction budget', 2) end, '', BUDGET)
  local result = pack(pcall(fn, ...))
  sethook()
  return unpack(result, 1, result.n)
end

local function copy(source, names)
  local t = {}
  for _, name in ipairs(names) do t[name] = source[name] end
  return t
end

local safe_string = copy(string, { 'byte', 'char', 'find', 'format', 'gmatch', 'gsub', 'len', 'lower', 'match', 'reverse', 'sub', 'upper' })
local safe_math = copy(math, { 'abs', 'acos', 'asin', 'atan', 'ceil', 'cos', 'deg', 'exp', 'floor', 'fmod', 'huge', 'log', 'max',
  'maxinteger', 'min', 'mininteger', 'modf', 'pi', 'rad', 'sin', 'sqrt', 'tan', 'tointeger', 'type', 'ult' })
local safe_table = copy(table, { 'concat', 'insert', 'move', 'pack', 'remove', 'sort', 'unpack' })
-- Method calls on strings (s:rep) use this table too.
getmetatable('').__index = safe_string

local function safe_setmetatable(t, mt)
  if type(mt) == 'table' and rawget(mt, '__gc') ~= nil then error('__gc is not allowed', 2) end
  return setmetatable(t, mt)
end

local function sandbox()
  return {
    assert = assert, error = error, ipairs = ipairs, next = next, pairs = pairs, select = select,
    tonumber = tonumber, tostring = tostring, type = type, rawequal = rawequal, rawget = rawget, rawlen = rawlen,
    rawset = rawset, setmetatable = safe_setmetatable, unpack = unpack,
    math = setmetatable({}, { __index = safe_math }), string = setmetatable({}, { __index = safe_string }),
    table = setmetatable({}, { __index = safe_table }),
  }
end

-- sources: module name -> source text (the gift's entry module and the modules of the Lua packages it depends on).
local function environment(sources)
  local env = sandbox()
  local loaded = {}
  env.require = function(name)
    local value = loaded[name]
    if value ~= nil then
      if value == loaded then error('circular require: ' .. tostring(name), 2) end
      return value
    end
    local source = sources[name]
    if type(source) ~= 'string' then error('module is not available to this gift: ' .. tostring(name), 2) end
    local chunk, message = load(source, '=' .. name, 't', env)
    if not chunk then error(message, 0) end
    loaded[name] = loaded
    value = chunk(name)
    if value == nil then value = true end
    loaded[name] = value
    return value
  end
  return env
end

-- Returns the entry module table or nil, message.
local function define(entry, sources)
  local env = environment(sources)
  local ok, module = guarded(env.require, entry)
  if not ok then return nil, module end
  if type(module) ~= 'table' then return nil, 'module must return a table' end
  if type(module.build) ~= 'function' then return nil, 'module has no build function' end
  if type(module.duration) ~= 'number' then return nil, 'module has no numeric duration' end
  if type(module.apiVersion) ~= 'number' then return nil, 'module has no apiVersion' end
  if module.resources ~= nil and type(module.resources) ~= 'table' then return nil, 'resources must be a list' end
  return module
end

local function pair(a, b, default)
  if type(a) == 'table' then return a[1] or a.x or default, a[2] or a.y or default end
  if a == nil then return default, default end
  return a, b == nil and a or b
end

-- Explicit cross-gift state; ordinary require caches remain isolated per definition.
-- Shared modules stay stateless. Playback data and Unity resource handles belong here or inside build().
local shared_states, shared_count = {}, 0
local function shared_state(key)
  if type(key) ~= 'string' or #key == 0 or #key > 96 then error('invalid shared state key', 2) end
  if shared_states[key] == nil then
    if shared_count >= 64 then error('too many shared states', 2) end
    shared_states[key] = {}
    shared_count = shared_count + 1
  end
  return shared_states[key]
end

local function facade(host)
  local g = { layer = 0 }
  function g.curve(node, at, d, a, b, c, dest, ease) return host:Curve(node, at, d, a[1], a[2], b[1], b[2], c[1], c[2], dest[1], dest[2], ease or 'Linear') end
  function g.concurrentScale(node, key, at, d, growth, limit) host:ConcurrentScale(node, key, at, d, growth, limit) end
  g.sharedState = shared_state
  function g.onStop(fn) host:OnStop(fn) end
  function g.onAudioStop(fn) host:OnAudioStop(fn) end
  function g.musicStart(clip, volume, loop) return host:MusicStart(clip, volume or 1, loop == true) end
  function g.musicAlive(handle) return host:MusicAlive(handle) end
  function g.musicRetain(handle) return host:MusicRetain(handle) end
  function g.musicFade(handle, volume, seconds, stop) host:MusicFade(handle, volume, seconds, stop == true) end
  function g.random() return host:Random() end
  function g.bounds() return host:Bounds() end
  function g.screen(u, v) return host:ScreenPoint(u or 0.5, v or 0.5) end
  function g.sender(ox, oy) return host:SenderPoint(ox or 0, oy or 0) end
  function g.target(ox, oy) return host:TargetPoint(ox or 0, oy or 0) end
  function g.senderSize() return host:SenderSize() end
  function g.targetSize() return host:TargetSize() end
  function g.node(parent, name, w, h) return host:Node(parent or 0, name or 'Node', w or 0, h or 0) end
  function g.image(parent, name, sprite, w, h) return host:Image(parent or 0, name or 'Image', sprite, w or 0, h or 0) end
  function g.joint(node, reach, direction) host:Joint(node, reach, direction or 1) end
  function g.screenEffect(parent, material) return host:ScreenEffect(parent or 0, material) end
  function g.screenParameter(node, parameter, at, d, from, to, ease) return host:ScreenParameter(node, parameter, at, d, from, to, ease or 'Linear') end
  function g.tint(node, at, d, from, to, ease) return host:Tint(node, at, d, from[1], from[2], from[3], to[1], to[2], to[3], ease or 'Linear') end
  function g.shape(parent, name, kind, w, h, color)
    local id = host:Shape(parent or 0, name or kind, kind, w or 0, h or 0)
    if color then host:SetColor(id, color[1], color[2], color[3], color[4] or 1) end
    return id
  end
  function g.instantiate(parent, prefab) return host:Instantiate(parent or 0, prefab) end
  function g.find(node, path) return host:Find(node, path) end
  -- Initial state. Keys: position, rotation, scale, size, pivot, alpha, color, visible, sprite, preserveAspect, fill,
  -- clockOffset. position, rotation, scale and alpha go to `channel` (default 0).
  function g.set(node, s, channel)
    local ch = channel or 0
    if s.position then local x, y = pair(s.position) host:SetPosition(node, ch, x, y) end
    if s.rotation then host:SetRotation(node, ch, s.rotation) end
    if s.scale then local x, y = pair(s.scale, nil, 1) host:SetScale(node, ch, x, y) end
    if s.size then host:SetSize(node, pair(s.size)) end
    if s.pivot then host:SetPivot(node, pair(s.pivot)) end
    if s.color then host:SetColor(node, s.color[1], s.color[2], s.color[3], s.color[4] or 1) end
    if s.alpha then host:SetAlpha(node, ch, s.alpha) end
    if s.sprite then host:SetSprite(node, s.sprite) end
    if s.preserveAspect ~= nil then host:SetPreserveAspect(node, s.preserveAspect) end
    if s.fill then host:SetFill(node, s.fill) end
    if s.clockOffset then host:SetClockOffset(node, s.clockOffset) end
    if s.visible ~= nil then host:SetVisible(node, s.visible) end
    return node
  end
  -- Tweens: (node, at, duration, from, to, ease, channel). Channel 0 is absolute; 1-7 add (position, rotation)
  -- or multiply (scale, alpha). Returns a tween handle for g.loop.
  function g.move(node, at, d, from, to, ease, ch)
    local x0, y0 = pair(from); local x1, y1 = pair(to)
    return host:Move(node, ch or 0, at, d, x0, y0, x1, y1, ease or 'Linear')
  end
  function g.rotate(node, at, d, from, to, ease, ch) return host:Rotate(node, ch or 0, at, d, from, to, ease or 'Linear') end
  function g.scale(node, at, d, from, to, ease, ch)
    local x0, y0 = pair(from, nil, 1); local x1, y1 = pair(to, nil, 1)
    return host:Scale(node, ch or 0, at, d, x0, y0, x1, y1, ease or 'Linear')
  end
  function g.fade(node, at, d, from, to, ease, ch) return host:Fade(node, ch or 0, at, d, from, to, ease or 'Linear') end
  function g.resize(node, at, d, from, to, ease)
    local w0, h0 = pair(from); local w1, h1 = pair(to)
    return host:Resize(node, at, d, w0, h0, w1, h1, ease or 'Linear')
  end
  function g.fill(node, at, d, from, to, ease) return host:Fill(node, at, d, from, to, ease or 'Linear') end
  function g.loop(tween, count, yoyo) host:Loop(tween, count, yoyo and true or false) return tween end
  function g.show(node, at, visible) host:Show(node, at, visible ~= false) end
  function g.hide(node, at) host:Show(node, at, false) end
  function g.swap(node, at, sprite) host:Swap(node, at, sprite) end
  function g.sound(at, clip, volume) host:Sound(at, clip, volume or 1) end
  function g.callback(at, fn) host:Callback(at, fn) end
  return g
end

local function build(module, host)
  return guarded(module.build, facade(host))
end

-- Stage callbacks run under the same budget.
local function call(fn)
  return guarded(fn)
end

-- The sorted string keys of the table a module returns: what a shared module offers (package lock file).
local function exports(name, sources)
  local env = environment(sources)
  local ok, module = guarded(env.require, name)
  if not ok then return nil, module end
  local names = {}
  if type(module) == 'table' then
    for key in pairs(module) do
      if type(key) == 'string' then names[#names + 1] = key end
    end
  end
  table.sort(names)
  return names
end

return define, build, call, exports
";

        private static LuaEnv _env;
        private static LuaFunction _define;
        private static LuaFunction _build;
        private static LuaFunction _call;
        private static LuaFunction _exports;
        private static float _nextTick;

        public static bool IsAvailable
        {
            get
            {
#if (UNITY_SERVER || UNITY_STANDALONE_LINUX) && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        private static void EnsureEnvironment()
        {
            if (_env != null) return;
            if (!IsAvailable) throw new InvalidOperationException("Gift scripts run only on clients.");
            // Reserve the bounded per-gift budget for ten concurrent gifts, with room for other UI tweens.
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(GiftScriptHost.MaxTweens * 12, 200);
            _env = new LuaEnv();
            using var loader = _env.LoadString(Bootstrap, "GiftScriptBootstrap");
            var functions = loader.Call(InstructionBudget);
            _define = (LuaFunction)functions[0];
            _build = (LuaFunction)functions[1];
            _call = (LuaFunction)functions[2];
            _exports = (LuaFunction)functions[3];
        }

        /// <summary>The names a module's returned table offers, sorted. Used by the build to compare package versions.</summary>
        public static IReadOnlyList<string> Exports(string module, IReadOnlyDictionary<string, string> sources)
        {
            EnsureEnvironment();
            using var table = _env.NewTable();
            foreach (var source in sources) table.Set(source.Key, source.Value);
            var results = _exports.Call(module, table);
            if (!(results?.Length > 0 && results[0] is LuaTable list))
                throw new GiftScriptException(results?.Length > 1 ? Convert.ToString(results[1]) : "Lua module failed to load: " + module);
            using (list)
            {
                var names = new List<string>();
                for (var i = 1; i <= list.Length; i++) names.Add(list.Get<int, string>(i));
                return names;
            }
        }

        /// <summary>Loads and validates a gift's modules. Throws with the script error when it is unusable.</summary>
        public static GiftScript Define(string entry, IReadOnlyDictionary<string, string> sources)
        {
            EnsureEnvironment();
            using var table = _env.NewTable();
            foreach (var source in sources) table.Set(source.Key, source.Value);
            var results = _define.Call(entry, table);
            if (!(results?.Length > 0 && results[0] is LuaTable module))
                throw new GiftScriptException(results?.Length > 1 ? results[1] as string : "Gift module failed to load.");
            try
            {
                var apiVersion = Convert.ToInt32(module.Get<double>("apiVersion"));
                if (apiVersion > GiftScriptHost.ApiVersion)
                    throw new GiftScriptException("Gift requires script API " + apiVersion + "; this page supports " + GiftScriptHost.ApiVersion + ".");
                var duration = (float)module.Get<double>("duration");
                if (duration <= 0f || duration >= GiftScriptHost.MaxDuration)
                    throw new GiftScriptException("Gift duration must be between 0 and " + GiftScriptHost.MaxDuration + " seconds.");
                var resources = new List<string>();
                using (var list = module.Get<LuaTable>("resources"))
                    if (list != null)
                        for (var i = 1; i <= list.Length; i++) resources.Add(list.Get<int, string>(i));
                return new GiftScript(module, duration, apiVersion, resources);
            }
            catch
            {
                module.Dispose();
                throw;
            }
        }

        internal static void Build(GiftScript script, GiftScriptHost host)
        {
            EnsureEnvironment();
            var results = _build.Call(script.Module, host);
            if (!(results?.Length > 0 && results[0] is bool ok && ok))
                throw new GiftScriptException(results?.Length > 1 ? Convert.ToString(results[1]) : "Gift build failed.");
        }

        internal static void Call(LuaFunction function)
        {
            var results = _call.Call(function);
            if (!(results?.Length > 0 && results[0] is bool ok && ok))
                throw new GiftScriptException(results?.Length > 1 ? Convert.ToString(results[1]) : "Gift callback failed.");
        }

        // Releases C# references held by Lua; called from playing effects.
        internal static void Tick()
        {
            if (_env == null || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 2f;
            _env.Tick();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            try
            {
                _define?.Dispose();
                _build?.Dispose();
                _call?.Dispose();
                _exports?.Dispose();
                _env?.Dispose();
            }
            catch (Exception exception) { Debug.LogWarning("Gift script environment was not released: " + exception.Message); }
            _define = _build = _call = _exports = null;
            _env = null;
        }
    }

    public sealed class GiftScriptException : Exception
    {
        public GiftScriptException(string message) : base(message) { }
    }
}
