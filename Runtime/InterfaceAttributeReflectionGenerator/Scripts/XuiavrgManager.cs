using Deconstruction.Type.Peculiarity;
using UnityEngine;

namespace XericUI.ReflectionUIGenerator
{
    /// <summary>
    /// 节点下生成反射属性管理器
    /// </summary>
    public class XuiavrgManager
    {
        
        /// <summary>
        /// 装填数据负载
        /// </summary>
        /// <param name="payload"></param>
        public void PerformSchemaDrivenHydration(object payload)
        {
            var type = payload.GetType();

            var attributes = type.GetCustomAttributes(true);
            foreach (var attribute in attributes)
            {
                if (attribute is not XuiavrgAttribute xuiavrgAttribute)
                    continue;
                
                
            }
        }
    }
}