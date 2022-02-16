﻿/*
 *  MIT License
 *  
 *  Copyright (c) 2021 LeonKou
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using Maikebing.Data.Taos;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace NetPro.Tdengine
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// ConnectionString formate: Data Source=taos;DataBase=db_20220120125645;Username=root;Password=taosdata;Port=6030
    /// </remarks>
    public static class TdengineServiceExtensions
    {
        /// <summary>
        /// AddTaos
        /// </summary>
        /// <param name="services"></param>
        /// <param name="taosOption"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static IServiceCollection AddTaos(this IServiceCollection services, TdengineOption tdengineOption)
        {
            services.AddSingleton(tdengineOption);
            var idleBus = new IdleBus<TaosConnection>();
            foreach (var item in tdengineOption.ConnectionString)
            {
                idleBus.Register(item.Key, () =>
                {
                    try
                    {
                        return new TaosConnection(item.Value);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                });
            }
            services.AddSingleton(idleBus);

            return services;
        }
    }
}