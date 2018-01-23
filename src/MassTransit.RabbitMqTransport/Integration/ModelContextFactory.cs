﻿// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
namespace MassTransit.RabbitMqTransport.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contexts;
    using GreenPipes;
    using GreenPipes.Agents;
    using Logging;
    using RabbitMQ.Client;


    public class ModelContextFactory :
        IPipeContextFactory<ModelContext>
    {
        static readonly ILog _log = Logger.Get<ModelContextFactory>();
        readonly IConnectionCache _connectionCache;
        readonly IRabbitMqHost _host;

        public ModelContextFactory(IConnectionCache connectionCache, IRabbitMqHost host)
        {
            _connectionCache = connectionCache;
            _host = host;
        }

        PipeContextHandle<ModelContext> IPipeContextFactory<ModelContext>.CreateContext(ISupervisor supervisor)
        {
            IAsyncPipeContextAgent<ModelContext> asyncContext = supervisor.AddAsyncContext<ModelContext>();

            var context = CreateModel(asyncContext, supervisor.Stopped);

            IPipeContextAgent<ModelContext> contextHandle = supervisor.AddContext(context);

            void HandleShutdown(object sender, ShutdownEventArgs args)
            {
                if (args.Initiator != ShutdownInitiator.Application)
                    contextHandle.Stop(args.ReplyText);
            }

            context.ContinueWith(task =>
            {
                task.Result.Model.ModelShutdown += HandleShutdown;

                contextHandle.Completed.ContinueWith(_ => task.Result.Model.ModelShutdown -= HandleShutdown);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return contextHandle;
        }

        ActivePipeContextHandle<ModelContext> IPipeContextFactory<ModelContext>.CreateActiveContext(ISupervisor supervisor,
            PipeContextHandle<ModelContext> context, CancellationToken cancellationToken)
        {
            return supervisor.AddActiveContext(context, CreateSharedModel(context.Context, cancellationToken));
        }

        async Task<ModelContext> CreateSharedModel(Task<ModelContext> context, CancellationToken cancellationToken)
        {
            var modelContext = await context.ConfigureAwait(false);

            var sharedModel = new SharedModelContext(modelContext, cancellationToken);

            return sharedModel;
        }

        async Task<ModelContext> CreateModel(IAsyncPipeContextAgent<ModelContext> asyncContext, CancellationToken cancellationToken)
        {
            IPipe<ConnectionContext> connectionPipe = Pipe.ExecuteAsync<ConnectionContext>(async connectionContext =>
            {
                if (_log.IsDebugEnabled)
                    _log.DebugFormat("Creating model: {0}", connectionContext.Description);

                try
                {
                    var model = await connectionContext.CreateModel().ConfigureAwait(false);

                    var modelContext = new RabbitMqModelContext(connectionContext, model, _host, cancellationToken);

                    await asyncContext.Created(modelContext).ConfigureAwait(false);

                    await asyncContext.Completed.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await asyncContext.CreateCanceled().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await asyncContext.CreateFaulted(exception).ConfigureAwait(false);
                }
            });

            _connectionCache.Send(connectionPipe, cancellationToken).ConfigureAwait(false);

            return await asyncContext.Context.ConfigureAwait(false);
        }
    }
}