using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.DTO
{
    public class TodoItemDto
    {
        public long Id { get; set; }
        public string? Todo { get; set; }
        public bool IsComplete { get; set; }
    }
}