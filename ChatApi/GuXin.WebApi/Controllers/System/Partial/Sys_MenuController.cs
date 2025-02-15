﻿using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using GuXin.Core.Enums;
using GuXin.Core.Filters;
using GuXin.Entity.DomainModels;
using GuXin.System.IServices;

namespace GuXin.System.Controllers
{
    public partial class Sys_MenuController
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpGet, HttpPost, Route("getTreeMenu")]
        //2019.10.24屏蔽用户查询自己权限菜单
        // [ApiActionPermission("Sys_Menu", ActionPermissionOptions.Search)]
        public async Task<IActionResult> GetTreeMenu()
        {
            return Json(await _service.GetCurrentMenuActionList());
        }
        [HttpPost, Route("getMenu")]
        [ApiActionPermission("Sys_Menu", ActionPermissionOptions.Search)]
        public async Task<IActionResult> GetMenu()
        {
            return Json(await _service.GetMenu());
        }

        [HttpPost, Route("getTreeItem")]
        [ApiActionPermission("Sys_Menu", "1", ActionPermissionOptions.Search)]
        public async Task<IActionResult> GetTreeItem(int menuId)
        {
            return Json(await _service.GetTreeItem(menuId));
        }

        //[ActionPermission("Sys_Menu", "1", ActionPermissionOptions.Add)]
        //只有角色ID为1的才能进行保存操作
        [HttpPost, Route("save"), ApiActionPermission(ActionRolePermission.SuperAdmin)]
        public async Task<ActionResult> Save([FromBody] Sys_Menu menu)
        {
            return Json(await _service.Save(menu));
        }

        /// <summary>
        /// 限制只能超级管理员才删除菜单 
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        [ApiActionPermission(ActionRolePermission.SuperAdmin)]
        [HttpPost, Route("delMenu")]
        public  async   Task<ActionResult> DelMenu(int menuId)
        {
            Core.Utilities.WebResponseContent webResponse = new Core.Utilities.WebResponseContent();
            var repository = Repositories.Sys_MenuRepository.Instance;
            if (await repository.ExistsAsync(x => x.ParentId == menuId))
            {  
                return Json(webResponse.Error("当前菜单存在子菜单,请先删除子菜单!"));
            }
            repository.Delete(new Sys_Menu()
            {
                Menu_Id = menuId
            },true);
            return Json(webResponse.OK("删除成功"));
        }

    }
}
