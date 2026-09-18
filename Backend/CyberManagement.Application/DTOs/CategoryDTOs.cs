using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Domain.Entities
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
