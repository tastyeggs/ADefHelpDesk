﻿//
// ADefHelpDesk.com
// Copyright (c) 2018
// by Michael Washington
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions 
// of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
//
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.Extensions.Options;
using AdefHelpDeskBase.Models;
using ADefHelpDeskApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using AdefHelpDeskBase.Models.DataContext;

namespace ADefHelpDeskApp.Controllers.WebApi
{
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "internal")]
    public class CategoryTreeController : Controller
    {        
        private IConfigurationRoot _configRoot { get; set; }
        private IMemoryCache _cache;

        public CategoryTreeController(
            IConfigurationRoot configRoot,
            IMemoryCache memoryCache)
        {
            _configRoot = configRoot;
            _cache = memoryCache;
        }

        // GET: api/CategoryTree/true
        [AllowAnonymous]
        [HttpGet("{UseCache}")]
        #region public List<CategoryDTO> GetCategoryTree([FromRoute] bool UseCache)
        public List<CategoryDTO> GetCategoryTree([FromRoute] bool UseCache)
        {
            return GetNodesMethod(UseCache, _cache, GetConnectionString());
        }
        #endregion

        // Methods

        #region public static List<CategoryDTO> GetNodesMethod(bool UseCache, IMemoryCache _cache, string strConectionString)
        public static List<CategoryDTO> GetNodesMethod(bool UseCache, IMemoryCache _cache, string strConectionString)
        {
            // Collection to hold final TreeNodes
            List<CategoryDTO> colTreeNodes = new List<CategoryDTO>();

            if (Convert.ToBoolean(UseCache))
            {
                // Look for tree in cache
                if (_cache.TryGetValue("TreeNodes", out colTreeNodes))
                {
                    return colTreeNodes;
                }
            }

            var optionsBuilder = new DbContextOptionsBuilder<ADefHelpDeskContext>();
            optionsBuilder.UseSqlServer(strConectionString);

            using (var context = new ADefHelpDeskContext(optionsBuilder.Options))
            {
                // This returns all Nodes in the database 
                colTreeNodes = new List<CategoryDTO>();
                var colNodes = (from objNode in context.AdefHelpDeskCategories
                                select new CategoryNode
                                {
                                    Id = objNode.CategoryId,
                                    NodeName = objNode.CategoryName,
                                    ParentId = objNode.ParentCategoryId,
                                    Selectable = objNode.Selectable,
                                    RequestorVisible = objNode.RequestorVisible
                                }).OrderBy(x => x.ParentId).ThenBy(y => y.NodeName).ToList();

                // Loop through Parent 'root' nodes
                // (meaning the NodeParentData is blank)
                foreach (CategoryNode objNode in colNodes
                    .Where(x => x.ParentId == null))
                {
                    // Create a new Node
                    CategoryDTO objNewNode = new CategoryDTO();

                    NodeDetailDTO objNewNodeDetail = new NodeDetailDTO();
                    objNewNodeDetail.categoryId = objNode.Id.ToString();
                    objNewNodeDetail.CheckboxChecked = false;
                    objNewNodeDetail.selectable = objNode.Selectable;
                    objNewNodeDetail.requestorVisible = objNode.RequestorVisible;
                    objNewNode.data = objNewNodeDetail;

                    objNewNode.categoryId = objNode.Id.ToString();
                    objNewNode.label = objNode.NodeName;
                    objNewNode.parentId = 0;
                    objNewNode.children = new List<CategoryDTO>();
                    objNewNode.selectable = true;

                    if (objNode.Selectable == true)
                    {
                        objNewNode.expandedIcon = "fa fa-fw fa fa-folder-open";
                        objNewNode.collapsedIcon = "fa fa-fw fa fa-folder";
                        objNewNode.type = "ShowCheckBox";
                    }

                    if (objNode.Selectable == false)
                    {
                        objNewNode.expandedIcon = "fas fa-th";
                        objNewNode.collapsedIcon = "fas fa-th";
                        objNewNode.type = "HideCheckBox";
                    }

                    colTreeNodes.Add(objNewNode);

                    // Add Child Nodes
                    AddChildren(colNodes, colTreeNodes, objNewNode);
                }
            }

            // Set cache options.
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Keep in cache for this time, reset time if accessed.
                .SetSlidingExpiration(TimeSpan.MaxValue);

            // Save data in cache.
            _cache.Set("TreeNodes", colTreeNodes, cacheEntryOptions);

            return colTreeNodes;
        } 
        #endregion

        // Utility

        #region AddChildren
        private static void AddChildren(
            List<CategoryNode> colNodeItemCollection,
            List<CategoryDTO> colTreeNodeCollection,
            CategoryDTO paramTreeNode)
        {
            // Get the children of the current item
            // This method may be called from the top level 
            // or recuresively by one of the child items
            var ChildResults = from objNode in colNodeItemCollection
                               where objNode.ParentId == Convert.ToInt32(paramTreeNode.categoryId)
                               select objNode;

            // Loop thru each Child of the current Node
            foreach (var objChild in ChildResults)
            {
                // Create a new Node
                var objNewNode = new CategoryDTO();

                objNewNode.categoryId = objChild.Id.ToString();
                objNewNode.label = objChild.NodeName;
                objNewNode.parentId = Convert.ToInt32(paramTreeNode.categoryId);
                objNewNode.children = new List<CategoryDTO>();
                objNewNode.selectable = true;

                if (objChild.Selectable == true)
                {
                    objNewNode.expandedIcon = "fa fa-fw fa fa-folder-open";
                    objNewNode.collapsedIcon = "fa fa-fw fa fa-folder";
                    objNewNode.type = "ShowCheckBox";
                }

                if (objChild.Selectable == false)
                {
                    objNewNode.expandedIcon = "fa-th";
                    objNewNode.collapsedIcon = "fa-th";
                    objNewNode.type = "HideCheckBox";
                }

                // Search for the Node in colTreeNodeCollection
                // By looping through each 'root' Node
                // (meaning the NodeParentData is blank)
                foreach (CategoryNode objNode in colNodeItemCollection
                    .Where(x => x.ParentId == null))
                {
                    // See if Parent is in the colTreeNodeCollection
                    CategoryDTO objParent =
                        colTreeNodeCollection.Where(x => x.categoryId == objNode.Id.ToString()).FirstOrDefault();

                    if (objParent != null) // Parent exists in the colTreeNodeCollection
                    {
                        // Get the Parent Node for the current Child Node
                        CategoryDTO objParentTreeNode = objParent.Descendants()
                            .Where(x => x.categoryId == paramTreeNode.categoryId).FirstOrDefault();

                        if (objParentTreeNode != null)
                        {
                            // Add the Child node to the Parent
                            NodeDetailDTO objNewNodeDetail = new NodeDetailDTO();
                            objNewNodeDetail.categoryId = objChild.Id.ToString();
                            objNewNodeDetail.CheckboxChecked = false;
                            objNewNodeDetail.selectable = objChild.Selectable;
                            objNewNodeDetail.requestorVisible = objChild.RequestorVisible;
                            objNewNode.data = objNewNodeDetail;

                            objParentTreeNode.children.Add(objNewNode);
                        }
                    }
                }

                //Recursively call the AddChildren method adding all children
                AddChildren(colNodeItemCollection, colTreeNodeCollection, objNewNode);
            }
        }
        #endregion

        #region private string GetConnectionString()
        private string GetConnectionString()
        {
            // Use this method to make sure we get the latest one
            string strConnectionString = "ERRROR:UNSET-CONECTION-STRING";

            try
            {
                strConnectionString = _configRoot.GetConnectionString("DefaultConnection");
            }
            catch
            {
                // Do nothing
            }

            return strConnectionString;
        }
        #endregion
    }
}
