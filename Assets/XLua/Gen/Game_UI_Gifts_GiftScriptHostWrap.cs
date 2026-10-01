#if USE_UNI_LUA
using LuaAPI = UniLua.Lua;
using RealStatePtr = UniLua.ILuaState;
using LuaCSFunction = UniLua.CSharpFunctionDelegate;
#else
using LuaAPI = XLua.LuaDLL.Lua;
using RealStatePtr = System.IntPtr;
using LuaCSFunction = XLua.LuaDLL.lua_CSFunction;
#endif

using XLua;
using System.Collections.Generic;


namespace XLua.CSObjectWrap
{
    using Utils = XLua.Utils;
    public class GameUIGiftsGiftScriptHostWrap 
    {
        public static void __Register(RealStatePtr L)
        {
			ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
			System.Type type = typeof(Game.UI.Gifts.GiftScriptHost);
			Utils.BeginObjectRegister(type, L, translator, 0, 35, 0, 0);
			
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SenderPoint", _m_SenderPoint);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "TargetPoint", _m_TargetPoint);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SenderSize", _m_SenderSize);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "TargetSize", _m_TargetSize);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "ScreenPoint", _m_ScreenPoint);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Bounds", _m_Bounds);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Random", _m_Random);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Node", _m_Node);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Image", _m_Image);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Shape", _m_Shape);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Instantiate", _m_Instantiate);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Find", _m_Find);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetPosition", _m_SetPosition);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetRotation", _m_SetRotation);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetScale", _m_SetScale);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetSize", _m_SetSize);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetPivot", _m_SetPivot);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetAlpha", _m_SetAlpha);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetColor", _m_SetColor);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetVisible", _m_SetVisible);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetSprite", _m_SetSprite);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetPreserveAspect", _m_SetPreserveAspect);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetFill", _m_SetFill);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "SetClockOffset", _m_SetClockOffset);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Move", _m_Move);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Rotate", _m_Rotate);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Scale", _m_Scale);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Fade", _m_Fade);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Resize", _m_Resize);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Fill", _m_Fill);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Loop", _m_Loop);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Show", _m_Show);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Swap", _m_Swap);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Sound", _m_Sound);
			Utils.RegisterFunc(L, Utils.METHOD_IDX, "Callback", _m_Callback);
			
			
			
			
			
			Utils.EndObjectRegister(type, L, translator, null, null,
			    null, null, null);

		    Utils.BeginClassRegister(type, L, __CreateInstance, 5, 0, 0);
			
			
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "ApiVersion", Game.UI.Gifts.GiftScriptHost.ApiVersion);
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "MaxNodes", Game.UI.Gifts.GiftScriptHost.MaxNodes);
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "MaxTweens", Game.UI.Gifts.GiftScriptHost.MaxTweens);
            Utils.RegisterObject(L, translator, Utils.CLS_IDX, "MaxDuration", Game.UI.Gifts.GiftScriptHost.MaxDuration);
            
			
			
			
			Utils.EndClassRegister(type, L, translator);
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int __CreateInstance(RealStatePtr L)
        {
            return LuaAPI.luaL_error(L, "Game.UI.Gifts.GiftScriptHost does not have a constructor!");
        }
        
		
        
		
        
        
        
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SenderPoint(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _offsetX = (float)LuaAPI.lua_tonumber(L, 2);
                    float _offsetY = (float)LuaAPI.lua_tonumber(L, 3);
                    float _x;
                    float _y;
                    
                    gen_to_be_invoked.SenderPoint( _offsetX, _offsetY, out _x, out _y );
                    LuaAPI.lua_pushnumber(L, _x);
                        
                    LuaAPI.lua_pushnumber(L, _y);
                        
                    
                    
                    
                    return 2;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_TargetPoint(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _offsetX = (float)LuaAPI.lua_tonumber(L, 2);
                    float _offsetY = (float)LuaAPI.lua_tonumber(L, 3);
                    float _x;
                    float _y;
                    
                    gen_to_be_invoked.TargetPoint( _offsetX, _offsetY, out _x, out _y );
                    LuaAPI.lua_pushnumber(L, _x);
                        
                    LuaAPI.lua_pushnumber(L, _y);
                        
                    
                    
                    
                    return 2;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SenderSize(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _width;
                    float _height;
                    
                    gen_to_be_invoked.SenderSize( out _width, out _height );
                    LuaAPI.lua_pushnumber(L, _width);
                        
                    LuaAPI.lua_pushnumber(L, _height);
                        
                    
                    
                    
                    return 2;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_TargetSize(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _width;
                    float _height;
                    
                    gen_to_be_invoked.TargetSize( out _width, out _height );
                    LuaAPI.lua_pushnumber(L, _width);
                        
                    LuaAPI.lua_pushnumber(L, _height);
                        
                    
                    
                    
                    return 2;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_ScreenPoint(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _u = (float)LuaAPI.lua_tonumber(L, 2);
                    float _v = (float)LuaAPI.lua_tonumber(L, 3);
                    float _x;
                    float _y;
                    
                    gen_to_be_invoked.ScreenPoint( _u, _v, out _x, out _y );
                    LuaAPI.lua_pushnumber(L, _x);
                        
                    LuaAPI.lua_pushnumber(L, _y);
                        
                    
                    
                    
                    return 2;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Bounds(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _xMin;
                    float _yMin;
                    float _xMax;
                    float _yMax;
                    
                    gen_to_be_invoked.Bounds( out _xMin, out _yMin, out _xMax, out _yMax );
                    LuaAPI.lua_pushnumber(L, _xMin);
                        
                    LuaAPI.lua_pushnumber(L, _yMin);
                        
                    LuaAPI.lua_pushnumber(L, _xMax);
                        
                    LuaAPI.lua_pushnumber(L, _yMax);
                        
                    
                    
                    
                    return 4;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Random(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    
                        var gen_ret = gen_to_be_invoked.Random(  );
                        LuaAPI.lua_pushnumber(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Node(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _parent = LuaAPI.xlua_tointeger(L, 2);
                    string _name = LuaAPI.lua_tostring(L, 3);
                    float _width = (float)LuaAPI.lua_tonumber(L, 4);
                    float _height = (float)LuaAPI.lua_tonumber(L, 5);
                    
                        var gen_ret = gen_to_be_invoked.Node( _parent, _name, _width, _height );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Image(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _parent = LuaAPI.xlua_tointeger(L, 2);
                    string _name = LuaAPI.lua_tostring(L, 3);
                    string _sprite = LuaAPI.lua_tostring(L, 4);
                    float _width = (float)LuaAPI.lua_tonumber(L, 5);
                    float _height = (float)LuaAPI.lua_tonumber(L, 6);
                    
                        var gen_ret = gen_to_be_invoked.Image( _parent, _name, _sprite, _width, _height );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Shape(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _parent = LuaAPI.xlua_tointeger(L, 2);
                    string _name = LuaAPI.lua_tostring(L, 3);
                    string _kind = LuaAPI.lua_tostring(L, 4);
                    float _width = (float)LuaAPI.lua_tonumber(L, 5);
                    float _height = (float)LuaAPI.lua_tonumber(L, 6);
                    
                        var gen_ret = gen_to_be_invoked.Shape( _parent, _name, _kind, _width, _height );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Instantiate(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _parent = LuaAPI.xlua_tointeger(L, 2);
                    string _prefab = LuaAPI.lua_tostring(L, 3);
                    
                        var gen_ret = gen_to_be_invoked.Instantiate( _parent, _prefab );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Find(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    string _path = LuaAPI.lua_tostring(L, 3);
                    
                        var gen_ret = gen_to_be_invoked.Find( _node, _path );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetPosition(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _x = (float)LuaAPI.lua_tonumber(L, 4);
                    float _y = (float)LuaAPI.lua_tonumber(L, 5);
                    
                    gen_to_be_invoked.SetPosition( _node, _channel, _x, _y );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetRotation(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _z = (float)LuaAPI.lua_tonumber(L, 4);
                    
                    gen_to_be_invoked.SetRotation( _node, _channel, _z );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetScale(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _x = (float)LuaAPI.lua_tonumber(L, 4);
                    float _y = (float)LuaAPI.lua_tonumber(L, 5);
                    
                    gen_to_be_invoked.SetScale( _node, _channel, _x, _y );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetSize(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _width = (float)LuaAPI.lua_tonumber(L, 3);
                    float _height = (float)LuaAPI.lua_tonumber(L, 4);
                    
                    gen_to_be_invoked.SetSize( _node, _width, _height );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetPivot(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _x = (float)LuaAPI.lua_tonumber(L, 3);
                    float _y = (float)LuaAPI.lua_tonumber(L, 4);
                    
                    gen_to_be_invoked.SetPivot( _node, _x, _y );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetAlpha(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _alpha = (float)LuaAPI.lua_tonumber(L, 4);
                    
                    gen_to_be_invoked.SetAlpha( _node, _channel, _alpha );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetColor(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _r = (float)LuaAPI.lua_tonumber(L, 3);
                    float _g = (float)LuaAPI.lua_tonumber(L, 4);
                    float _b = (float)LuaAPI.lua_tonumber(L, 5);
                    float _a = (float)LuaAPI.lua_tonumber(L, 6);
                    
                    gen_to_be_invoked.SetColor( _node, _r, _g, _b, _a );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetVisible(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    bool _visible = LuaAPI.lua_toboolean(L, 3);
                    
                    gen_to_be_invoked.SetVisible( _node, _visible );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetSprite(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    string _sprite = LuaAPI.lua_tostring(L, 3);
                    
                    gen_to_be_invoked.SetSprite( _node, _sprite );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetPreserveAspect(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    bool _preserve = LuaAPI.lua_toboolean(L, 3);
                    
                    gen_to_be_invoked.SetPreserveAspect( _node, _preserve );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetFill(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _fill = (float)LuaAPI.lua_tonumber(L, 3);
                    
                    gen_to_be_invoked.SetFill( _node, _fill );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_SetClockOffset(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _seconds = (float)LuaAPI.lua_tonumber(L, 3);
                    
                    gen_to_be_invoked.SetClockOffset( _node, _seconds );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Move(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _at = (float)LuaAPI.lua_tonumber(L, 4);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 5);
                    float _x0 = (float)LuaAPI.lua_tonumber(L, 6);
                    float _y0 = (float)LuaAPI.lua_tonumber(L, 7);
                    float _x1 = (float)LuaAPI.lua_tonumber(L, 8);
                    float _y1 = (float)LuaAPI.lua_tonumber(L, 9);
                    string _ease = LuaAPI.lua_tostring(L, 10);
                    
                        var gen_ret = gen_to_be_invoked.Move( _node, _channel, _at, _duration, _x0, _y0, _x1, _y1, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Rotate(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _at = (float)LuaAPI.lua_tonumber(L, 4);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 5);
                    float _from = (float)LuaAPI.lua_tonumber(L, 6);
                    float _to = (float)LuaAPI.lua_tonumber(L, 7);
                    string _ease = LuaAPI.lua_tostring(L, 8);
                    
                        var gen_ret = gen_to_be_invoked.Rotate( _node, _channel, _at, _duration, _from, _to, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Scale(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _at = (float)LuaAPI.lua_tonumber(L, 4);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 5);
                    float _x0 = (float)LuaAPI.lua_tonumber(L, 6);
                    float _y0 = (float)LuaAPI.lua_tonumber(L, 7);
                    float _x1 = (float)LuaAPI.lua_tonumber(L, 8);
                    float _y1 = (float)LuaAPI.lua_tonumber(L, 9);
                    string _ease = LuaAPI.lua_tostring(L, 10);
                    
                        var gen_ret = gen_to_be_invoked.Scale( _node, _channel, _at, _duration, _x0, _y0, _x1, _y1, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Fade(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    int _channel = LuaAPI.xlua_tointeger(L, 3);
                    float _at = (float)LuaAPI.lua_tonumber(L, 4);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 5);
                    float _from = (float)LuaAPI.lua_tonumber(L, 6);
                    float _to = (float)LuaAPI.lua_tonumber(L, 7);
                    string _ease = LuaAPI.lua_tostring(L, 8);
                    
                        var gen_ret = gen_to_be_invoked.Fade( _node, _channel, _at, _duration, _from, _to, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Resize(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _at = (float)LuaAPI.lua_tonumber(L, 3);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 4);
                    float _w0 = (float)LuaAPI.lua_tonumber(L, 5);
                    float _h0 = (float)LuaAPI.lua_tonumber(L, 6);
                    float _w1 = (float)LuaAPI.lua_tonumber(L, 7);
                    float _h1 = (float)LuaAPI.lua_tonumber(L, 8);
                    string _ease = LuaAPI.lua_tostring(L, 9);
                    
                        var gen_ret = gen_to_be_invoked.Resize( _node, _at, _duration, _w0, _h0, _w1, _h1, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Fill(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _at = (float)LuaAPI.lua_tonumber(L, 3);
                    float _duration = (float)LuaAPI.lua_tonumber(L, 4);
                    float _from = (float)LuaAPI.lua_tonumber(L, 5);
                    float _to = (float)LuaAPI.lua_tonumber(L, 6);
                    string _ease = LuaAPI.lua_tostring(L, 7);
                    
                        var gen_ret = gen_to_be_invoked.Fill( _node, _at, _duration, _from, _to, _ease );
                        LuaAPI.xlua_pushinteger(L, gen_ret);
                    
                    
                    
                    return 1;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Loop(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _tween = LuaAPI.xlua_tointeger(L, 2);
                    int _count = LuaAPI.xlua_tointeger(L, 3);
                    bool _yoyo = LuaAPI.lua_toboolean(L, 4);
                    
                    gen_to_be_invoked.Loop( _tween, _count, _yoyo );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Show(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _at = (float)LuaAPI.lua_tonumber(L, 3);
                    bool _visible = LuaAPI.lua_toboolean(L, 4);
                    
                    gen_to_be_invoked.Show( _node, _at, _visible );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Swap(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    int _node = LuaAPI.xlua_tointeger(L, 2);
                    float _at = (float)LuaAPI.lua_tonumber(L, 3);
                    string _sprite = LuaAPI.lua_tostring(L, 4);
                    
                    gen_to_be_invoked.Swap( _node, _at, _sprite );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Sound(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _at = (float)LuaAPI.lua_tonumber(L, 2);
                    string _clip = LuaAPI.lua_tostring(L, 3);
                    float _volume = (float)LuaAPI.lua_tonumber(L, 4);
                    
                    gen_to_be_invoked.Sound( _at, _clip, _volume );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        [MonoPInvokeCallbackAttribute(typeof(LuaCSFunction))]
        static int _m_Callback(RealStatePtr L)
        {
		    try {
            
                ObjectTranslator translator = ObjectTranslatorPool.Instance.Find(L);
            
            
                Game.UI.Gifts.GiftScriptHost gen_to_be_invoked = (Game.UI.Gifts.GiftScriptHost)translator.FastGetCSObj(L, 1);
            
            
                
                {
                    float _at = (float)LuaAPI.lua_tonumber(L, 2);
                    XLua.LuaFunction _function = (XLua.LuaFunction)translator.GetObject(L, 3, typeof(XLua.LuaFunction));
                    
                    gen_to_be_invoked.Callback( _at, _function );
                    
                    
                    
                    return 0;
                }
                
            } catch(System.Exception gen_e) {
                return LuaAPI.luaL_error(L, "c# exception:" + gen_e);
            }
            
        }
        
        
        
        
        
        
		
		
		
		
    }
}
