﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using VstsDemoBuilder.ServiceInterfaces;

namespace VstsDemoBuilder.Services
{
    public class ProjectService : IProjectService
    {
        public string GetMessage()
        {
            return "Welcome";
        }
    }
}