using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class CategoryDTOs
    {
        public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateCategoryDTOs
    {
        public string CategoryName { get; set; } = string.Empty;
    }

    public class UpdateCategoryDTOs
    {
        //public long CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
