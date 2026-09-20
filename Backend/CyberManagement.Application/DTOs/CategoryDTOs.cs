using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.DTOs
{
    public class CategoryDTOs
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class CreateCategoryDTOs
    {
        public string CategoryName { get; set; } = string.Empty;
    }

    public class UpdateCategoryDTOs
    {
        //public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
