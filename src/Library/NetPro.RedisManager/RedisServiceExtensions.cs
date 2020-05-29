﻿using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Collections.Generic;

namespace NetPro.RedisManager
{
    public static class RedisServiceExtensions
    {
        public static IServiceCollection AddRedisManager(this IServiceCollection services, IConfiguration configuration)
        {
            var option = configuration.GetSection(nameof(RedisCacheOption)).Get<RedisCacheOption>();

            services.AddRedisManager(option);
            return services;
        }

        public static IServiceCollection AddRedisManager(this IServiceCollection services, RedisCacheOption redisCacheOption)
        {
            var redisCacheComponent = redisCacheOption?.RedisComponent ?? RedisCacheComponentEnum.NullRedis;

            switch (redisCacheComponent)
            {
                case RedisCacheComponentEnum.CSRedisCore:
                    services.AddCsRedis(redisCacheOption);
                    break;
                case RedisCacheComponentEnum.StackExchangeRedis:
                    services.AddStackExchangeRedis(redisCacheOption);
                    break;
                case RedisCacheComponentEnum.NullRedis:
                    services.AddSingleton<IRedisManager, NullCache>();
                    break;
                default:
                    services.AddSingleton(redisCacheOption);
                    services.AddSingleton<IRedisManager, NullCache>();
                    break;
            }
            return services;
        }

        public static IServiceCollection AddCsRedis(this IServiceCollection services, RedisCacheOption option)
        {
            services.AddSingleton(option);
            List<string> csredisConns = new List<string>();
            string password = option.Password;
            int defaultDb = option.Database;
            string ssl = option.SslHost;
            string keyPrefix = option.DefaultCustomKey;
            int writeBuffer = 10240;
            int poolsize = option.PoolSize == 0 ? 10 : option.PoolSize;
            int timeout = option.ConnectionTimeout;
            foreach (var e in option.Endpoints)
            {
                string server = e.Host;
                int port = e.Port;
                if (string.IsNullOrWhiteSpace(server) || port <= 0) { continue; }
                csredisConns.Add($"{server}:{port},password={password},defaultDatabase={defaultDb},poolsize={poolsize},ssl={ssl},writeBuffer={writeBuffer},prefix={keyPrefix},preheat={option.Preheat},idleTimeout={timeout}");
            }

            CSRedis.CSRedisClient csredis;

            try
            {
                csredis = new CSRedis.CSRedisClient(null, csredisConns.ToArray());
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Check the configuration for redis;{ex}");
            }

            RedisHelper.Initialization(csredis);
            services.AddScoped<IRedisManager, CsRedisManager>();
            return services;
        }

        public static IServiceCollection AddCsRedis(this IServiceCollection services, IConfiguration config)
        {
            var option = new RedisCacheOption(config);
            services.AddCsRedis(option);
            return services;
        }

        public static IServiceCollection AddStackExchangeRedis(this IServiceCollection services, IConfiguration config)
        {
            var redisOption = new RedisCacheOption(config);

            services.AddStackExchangeRedis(redisOption);
            return services;
        }

        public static IServiceCollection AddStackExchangeRedis(this IServiceCollection services, RedisCacheOption redisOption)
        {
            services.AddSingleton(redisOption);
            var configurationOptions = new ConfigurationOptions
            {
                KeepAlive = 180,
                ConnectTimeout = redisOption.ConnectionTimeout,
                Password = redisOption.Password,
                Ssl = redisOption.IsSsl,
                SslHost = redisOption.SslHost,
                AbortOnConnectFail = false,
                AsyncTimeout = redisOption.ConnectionTimeout,
            };

            foreach (var endpoint in redisOption.Endpoints)
            {
                configurationOptions.EndPoints.Add(endpoint.Host, endpoint.Port);
            }

            var connect = ConnectionMultiplexer.Connect(configurationOptions);
            //注册如下事件
            connect.ConnectionFailed += MuxerConnectionFailed;
            connect.ConnectionRestored += MuxerConnectionRestored;
            connect.ErrorMessage += MuxerErrorMessage;
            connect.ConfigurationChanged += MuxerConfigurationChanged;
            connect.HashSlotMoved += MuxerHashSlotMoved;
            connect.InternalError += MuxerInternalError;

            services.Add(ServiceDescriptor.Singleton(connect));
            services.AddSingleton<IRedisManager, StackExchangeRedisManager>();
            return services;
        }

        #region 事件

        /// <summary>
        /// 配置更改时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerConfigurationChanged(object sender, EndPointEventArgs e)
        {
            Console.WriteLine("Configuration changed: " + e.EndPoint);
        }

        /// <summary>
        /// 发生错误时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerErrorMessage(object sender, RedisErrorEventArgs e)
        {
            Console.WriteLine("ErrorMessage: " + e.Message);
        }

        /// <summary>
        /// 重新建立连接之前的错误
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerConnectionRestored(object sender, ConnectionFailedEventArgs e)
        {
            Console.WriteLine("ConnectionRestored: " + e.EndPoint);
        }

        /// <summary>
        /// 连接失败 ， 如果重新连接成功你将不会收到这个通知
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            Console.WriteLine("Retry：Endpoint failed: " + e.EndPoint + ", " + e.FailureType + (e.Exception == null ? "" : (", " + e.Exception.Message)));
        }

        /// <summary>
        /// 更改集群
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerHashSlotMoved(object sender, HashSlotMovedEventArgs e)
        {
            Console.WriteLine("HashSlotMoved:NewEndPoint" + e.NewEndPoint + ", OldEndPoint" + e.OldEndPoint);
        }

        /// <summary>
        /// redis类库错误
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MuxerInternalError(object sender, InternalErrorEventArgs e)
        {
            Console.WriteLine("InternalError:Message" + e.Exception.Message);
        }

        #endregion 事件
    }
}
