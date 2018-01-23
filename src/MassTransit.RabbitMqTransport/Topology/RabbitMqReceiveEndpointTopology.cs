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
namespace MassTransit.RabbitMqTransport.Topology
{
    using System;
    using Builders;
    using EndpointSpecifications;
    using MassTransit.Topology;
    using Transport;
    using Transports;


    public class RabbitMqReceiveEndpointTopology :
        ReceiveEndpointTopology,
        IRabbitMqReceiveEndpointTopology
    {
        readonly RabbitMqHost _host;
        readonly BusHostCollection<RabbitMqHost> _hosts;
        readonly Lazy<ISendTransportProvider> _sendTransportProvider;
        readonly Lazy<IPublishTransportProvider> _publishTransportProvider;
        readonly IRabbitMqPublishTopology _publishTopology;

        public RabbitMqReceiveEndpointTopology(IRabbitMqEndpointConfiguration configuration, Uri inputAddress, RabbitMqHost host, BusHostCollection<RabbitMqHost> hosts,
            BrokerTopology brokerTopology)
            : base(configuration, inputAddress, host.Address)
        {
            _host = host;
            BrokerTopology = brokerTopology;

            _hosts = hosts;
            _publishTopology = configuration.Topology.Publish;

            _sendTransportProvider = new Lazy<ISendTransportProvider>(CreateSendTransportProvider);
            _publishTransportProvider = new Lazy<IPublishTransportProvider>(CreatePublishTransportProvider);
        }

        public BrokerTopology BrokerTopology { get; }

        public ISendEndpointProvider CreateSendEndpointProvider(ReceiveContext receiveContext)
        {
            var transportProvider = new ReceiveContextSendTransportProvider(_hosts, receiveContext);

            return new SendEndpointProvider(transportProvider, SendObservers, Serializer, InputAddress, SendPipe);
        }

        public IPublishEndpointProvider CreatePublishEndpointProvider(ReceiveContext receiveContext)
        {
            var transportProivder = new ReceiveContextPublishTransportProvider(receiveContext, _host, _publishTopology);

            return new PublishEndpointProvider(transportProivder, _host.Address, PublishObservers, SendObservers, Serializer, InputAddress, PublishPipe, _publishTopology);
        }

        ISendTransportProvider CreateSendTransportProvider()
        {
            return new SendTransportProvider(_hosts);
        }

        IPublishTransportProvider CreatePublishTransportProvider()
        {
            return new PublishTransportProvider(_host, _publishTopology);
        }

        protected override ISendEndpointProvider CreateSendEndpointProvider()
        {
            return new SendEndpointProvider(_sendTransportProvider.Value, SendObservers, Serializer, InputAddress, SendPipe);
        }

        protected override IPublishEndpointProvider CreatePublishEndpointProvider()
        {
            return new PublishEndpointProvider(_publishTransportProvider.Value, _host.Address, PublishObservers, SendObservers, Serializer, InputAddress, PublishPipe,
                _publishTopology);
        }
    }
}