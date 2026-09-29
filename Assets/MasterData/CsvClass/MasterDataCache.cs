using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterData
{
    public class MasterDataCache
    {
        private static Dictionary<string, ScriptableObject> cachesByName = new Dictionary<string, ScriptableObject>();

        public static T GetCache<T>() where T : ScriptableObject
        {
            string cacheName = typeof(T).Name;
            if (!cachesByName.ContainsKey(cacheName))
            {
                throw new Exception(cacheName + " not loaded");
            }
            return cachesByName[cacheName] as T;
        }

		public static T GetCache<T>(string _name) where T : ScriptableObject {
			string cacheName = _name;
			if (!cachesByName.ContainsKey(cacheName)) {
				throw new Exception(cacheName + " not loaded");
			}
			return cachesByName[cacheName] as T;
		}

        public static void ClearCache()
        {
            cachesByName.Clear();
        }

        public static void Cache<T>(ScriptableObject _data) where T : ScriptableObject
        {
            string cacheName = typeof(T).Name;
            if (cachesByName.ContainsKey(cacheName))
            {
                cachesByName[cacheName] = _data as ScriptableObject;
            }
            else
            {
                cachesByName.Add(cacheName, _data as ScriptableObject);
            }
        }

        public static void CacheByName(string _cacheName, ScriptableObject _data)
        {
            if (cachesByName.ContainsKey(_cacheName))
            {
                cachesByName[_cacheName] = _data as ScriptableObject;
            }
            else
            {
                cachesByName.Add(_cacheName, _data as ScriptableObject);
            }
        }

        public static bool IsCached(string _cacheName)
        {
            return cachesByName.ContainsKey(_cacheName);
        }
    }
}
