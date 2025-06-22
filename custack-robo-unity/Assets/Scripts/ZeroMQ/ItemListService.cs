using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace ZeroMQ
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Messages;
    public class ItemListService : MessageService
    {
        [SerializeField, ReadOnly]
        public Header Header { get; private set; } = new();
        [SerializeField, ReadOnly]
        public List<string> Items { get; private set; } = new();

        public Func<List<string>, bool> ProcessData { private get; set; } = null;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
        }

        protected override string ProcessResponse(string message)
        {
            var req_json = JsonSerializer.Deserialize<ItemListRequest>(message);
            Header = req_json.header;
            Items = req_json.items;

            var res = new ItemListResponse();
            res.success = true;
            res.message = "OK";

            var res_json = JsonSerializer.Serialize(res);

            if (ProcessData != null)
            {
                var success = ProcessData.Invoke(Items);
            }

            return res_json;
        }
    }
}
