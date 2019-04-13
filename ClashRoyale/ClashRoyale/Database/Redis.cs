﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ClashRoyale.Core;
using ClashRoyale.Logic;
using ClashRoyale.Logic.Clan;
using Newtonsoft.Json;
using SharpRaven.Data;
using StackExchange.Redis;

namespace ClashRoyale.Database
{
    public class Redis
    {
        private static IDatabase _players;
        private static IDatabase _alliances;
        private static IServer _server;

        private static ConnectionMultiplexer _connection;

        public Redis()
        {
            try
            {
                var config = new ConfigurationOptions
                {
                    AllowAdmin = true,
                    ConnectTimeout = 10000,
                    ConnectRetry = 10,
                    HighPrioritySocketThreads = true,
                    Password = Resources.Configuration.RedisPassword
                };

                config.EndPoints.Add(Resources.Configuration.RedisServer, 6379);

                _connection = ConnectionMultiplexer.Connect(config);

                _players = _connection.GetDatabase(0);
                _alliances = _connection.GetDatabase(1);
                _server = _connection.GetServer(Resources.Configuration.RedisServer, 6379);

                Logger.Log($"Successfully loaded Redis with {CachedPlayers()} player(s) & {CachedAlliances()} clan(s)",
                    GetType());
            }
            catch (Exception exception)
            {
                Logger.Log(exception, GetType(), ErrorLevel.Error);
            }
        }

        /// <summary>
        /// Returns true wether the client is connected
        /// </summary>
        public static bool IsConnected => _server != null;

        /// <summary>
        /// Cache a player
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static async Task Cache(Player player)
        {
            if (player == null) return;

            try
            {
                await _players.StringSetAsync(player.Home.Id.ToString(),
                    JsonConvert.SerializeObject(player, Configuration.JsonSettings), TimeSpan.FromHours(4));
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }
        }

        /// <summary>
        /// Cache an alliance
        /// </summary>
        /// <param name="alliance"></param>
        /// <returns></returns>
        public static async Task Cache(Alliance alliance)
        {
            if (alliance == null) return;

            try
            {
                await _alliances.StringSetAsync(alliance.Id.ToString(),
                    JsonConvert.SerializeObject(alliance, Configuration.JsonSettings), TimeSpan.FromHours(4));
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }
        }

        /// <summary>
        /// Uncache a player 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task UncachePlayer(long id)
        {
            try
            {
                await _players.KeyDeleteAsync(id.ToString());
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }
        }

        /// <summary>
        /// Uncache an alliance
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task UncacheAlliance(long id)
        {
            try
            {
                await _alliances.KeyDeleteAsync(id.ToString());
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }
        }

        /// <summary>
        /// Get the player from the cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<Player> GetPlayer(long id)
        {
            try
            {
                var data = await _players.StringGetAsync(id.ToString());

                if (!string.IsNullOrEmpty(data))
                    return JsonConvert.DeserializeObject<Player>(data, Configuration.JsonSettings);

                var player = await PlayerDb.Get(id);
                await Cache(player);
                return player;
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }

            return null;
        }

        /// <summary>
        /// Get an alliance from the cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<Alliance> GetAlliance(long id)
        {
            try
            {
                var data = await _alliances.StringGetAsync(id.ToString());

                if (!string.IsNullOrEmpty(data))
                    return JsonConvert.DeserializeObject<Alliance>(data, Configuration.JsonSettings);

                var alliance = await AllianceDb.Get(id);
                await Cache(alliance);
                return alliance;
            }
            catch (Exception exception)
            {
                Logger.Log(exception, null, ErrorLevel.Error);
            }

            return null;
        }

        /// <summary>
        /// Get a random alliance from the cache
        /// </summary>
        /// <returns></returns>
        public static async Task<Alliance> GetRandomAlliance()
        {
            return await GetAlliance(long.Parse(await _alliances.KeyRandomAsync()));
        }

        /// <summary>
        /// Returns the amount of cached players
        /// </summary>
        /// <returns></returns>
        public static int CachedPlayers()
        {
            try
            {
                return Convert.ToInt32(
                    _connection.GetServer(Resources.Configuration.RedisServer, 6379).Info("keyspace")[0]
                        .ElementAt(_players.Database)
                        .Value
                        .Split(new[] {"keys="}, StringSplitOptions.None)[1]
                        .Split(new[] {",expires="}, StringSplitOptions.None)[0]);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns the amount of cached alliances 
        /// </summary>
        /// <returns></returns>
        public static int CachedAlliances()
        {
            try
            {
                return Convert.ToInt32(
                    _connection.GetServer(Resources.Configuration.RedisServer, 6379).Info("keyspace")[0]
                        .ElementAt(_alliances.Database)
                        .Value
                        .Split(new[] {"keys="}, StringSplitOptions.None)[1]
                        .Split(new[] {",expires="}, StringSplitOptions.None)[0]);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}