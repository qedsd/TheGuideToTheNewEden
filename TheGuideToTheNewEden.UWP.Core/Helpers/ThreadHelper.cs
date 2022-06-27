using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Helpers
{
    static class ThreadHelper
    {
        /// <summary>
        /// 并行处理集合中的每一项
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>异步任务</returns>
        public static Task RunAsync<T>(IEnumerable<T> source, Action<T> action) => RunAsync(source, Environment.ProcessorCount, action);

        /// <summary>
        /// 并行处理集合中的每一项
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>异步任务</returns>
        public static Task RunAsync<T>(IEnumerable<T> source, int threadLimit, Action<T> action)
        {
            ConcurrentQueue<T> datas = new ConcurrentQueue<T>(source);
            var tasks = Enumerable.Range(0, threadLimit).Select(t => Task.Run(() =>
            {
                while (datas.TryDequeue(out var data))
                {
                    action(data);
                }
            }));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// 并行处理集合中的每一项
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>异步任务</returns>
        public static Task RunAsync<T>(IEnumerable<T> source, Func<T, Task> action) => RunAsync(source, Environment.ProcessorCount, action);

        /// <summary>
        /// 并行处理集合中的每一项
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>异步任务</returns>
        public static Task RunAsync<T>(IEnumerable<T> source, int threadLimit, Func<T, Task> action)
        {
            ConcurrentQueue<T> datas = new ConcurrentQueue<T>(source);
            var tasks = Enumerable.Range(0, threadLimit).Select(t => Task.Run(async () =>
            {
                while (datas.TryDequeue(out var data))
                {
                    await action(data);
                }
            }));
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// 并行处理集合中的每一项，并将返回每一项的处理结果集合
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>处理结果集合的异步任务</returns>
        public static async Task<IReadOnlyList<K>> RunAsync<T, K>(IEnumerable<T> source, Func<T, K> action) => await RunAsync(source, Environment.ProcessorCount, action);

        /// <summary>
        /// 并行处理集合中的每一项，并将返回每一项的处理结果集合
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>处理结果集合的异步任务</returns>
        public static async Task<IReadOnlyList<K>> RunAsync<T, K>(IEnumerable<T> source, int threadLimit, Func<T, K> action)
        {
            ConcurrentQueue<K> result = new ConcurrentQueue<K>();
            ConcurrentQueue<T> datas = new ConcurrentQueue<T>(source);
            var tasks = Enumerable.Range(0, threadLimit).Select(t => Task.Run(() =>
            {
                while (datas.TryDequeue(out var data))
                {
                    result.Enqueue(action(data));
                }
            }));
            await Task.WhenAll(tasks);
            return result.ToList();
        }

        public static IReadOnlyList<K> Run<T, K>(IEnumerable<T> source, Func<T, K> action) => Run(source, Environment.ProcessorCount, action);

        public static IReadOnlyList<K> Run<T, K>(IEnumerable<T> source, int threadLimit, Func<T, K> action)
        {
            ConcurrentQueue<K> result = new ConcurrentQueue<K>();
            ConcurrentQueue<T> datas = new ConcurrentQueue<T>(source);
            var tasks = Enumerable.Range(0, threadLimit).Select(t => Task.Run(() =>
            {
                while (datas.TryDequeue(out var data))
                {
                    result.Enqueue(action(data));
                }
            }));
            Task.WaitAll(tasks.ToArray());
            return result.ToList();
        }

        /// <summary>
        /// 并行处理集合中的每一项，并将返回每一项的处理结果集合
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>处理结果集合的异步任务</returns>
        public static async Task<IReadOnlyList<K>> RunAsync<T, K>(IEnumerable<T> source, Func<T, Task<K>> action) => await RunAsync(source, Environment.ProcessorCount, action);

        /// <summary>
        /// 并行处理集合中的每一项，并将返回每一项的处理结果集合
        /// </summary>
        /// <typeparam name="T">集合类型</typeparam>
        /// <param name="source">需要并行处理的集合</param>
        /// <param name="threadLimit">最大线程数限制</param>
        /// <param name="action">处理集合元素的委托</param>
        /// <returns>处理结果集合的异步任务</returns>
        public static async Task<IReadOnlyList<K>> RunAsync<T, K>(IEnumerable<T> source, int threadLimit, Func<T, Task<K>> action)
        {
            ConcurrentQueue<K> result = new ConcurrentQueue<K>();
            ConcurrentQueue<T> datas = new ConcurrentQueue<T>(source);
            var tasks = Enumerable.Range(0, threadLimit).Select(t => Task.Run(async () =>
            {
                while (datas.TryDequeue(out var data))
                {
                    result.Enqueue(await action(data));
                }
            }));
            await Task.WhenAll(tasks);
            return result.ToList();
        }
    }
}
