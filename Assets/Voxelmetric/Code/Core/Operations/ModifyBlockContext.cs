using System;
using UnityEngine.Assertions;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core.Operations
{
    public class ModifyBlockContext
    {
        //! World this operation belong to
        private readonly World world;
        //! Action to perform once all child actions are finished
        private readonly Action<ModifyBlockContext> action;
        //! Block which is to be worked with
        private BlockData m_Block;
        public BlockData Block { get { return m_Block; } }
        //! Starting block position within chunk
        private int m_IndexFrom;
        public int IndexFrom { get { return m_IndexFrom; } }
        //! Ending block position within chunk
        private int m_IndexTo;
        public int IndexTo { get { return m_IndexTo; } }
        //! Parent action
        private int m_ChildActionsPending;
        //! If true we want to mark the block as modified
        public readonly bool setBlockModified;

        public ModifyBlockContext(Action<ModifyBlockContext> action, World world, int index, BlockData block, bool setBlockModified)
        {
            this.world = world;
            this.action = action;
            m_Block = block;
            m_IndexFrom = m_IndexTo = index;
            m_ChildActionsPending = 0;
            this.setBlockModified = setBlockModified;
        }

        public ModifyBlockContext(Action<ModifyBlockContext> action, World world, int indexFrom, int indexTo, BlockData block, bool setBlockModified)
        {
            this.world = world;
            this.action = action;
            m_Block = block;
            m_IndexFrom = indexFrom;
            m_IndexTo = indexTo;
            m_ChildActionsPending = 0;
            this.setBlockModified = setBlockModified;
        }

        public void RegisterChildAction()
        {
            ++m_ChildActionsPending;
        }

        public void ChildActionFinished()
        {
            // Once all child actions are performed register this action in the world
            --m_ChildActionsPending;
            Assert.IsTrue(m_ChildActionsPending >= 0);
            if (m_ChildActionsPending == 0)
                world.RegisterModifyRange(this);
        }

        public void PerformAction()
        {
            action(this);
        }
    }
}
