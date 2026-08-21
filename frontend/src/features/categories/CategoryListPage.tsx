import { useState } from 'react';
import { Table, Button, Modal, Popconfirm, message } from 'antd';
import { useListCategoriesQuery, useDeleteCategoryMutation } from './api';
import { buildCategoryTree } from './buildCategoryTree';
import type { CategoryTreeNode } from './buildCategoryTree';
import { CategoryForm } from './CategoryForm';
import type { Category } from './types';
import type { AppError } from '../../shared/lib/errors';

function collectParentKeys(nodes: CategoryTreeNode[]): number[] {
  const keys: number[] = [];
  for (const node of nodes) {
    if (node.children?.length) {
      keys.push(node.id, ...collectParentKeys(node.children));
    }
  }
  return keys;
}

export function CategoryListPage() {
  const { data: categories, isLoading } = useListCategoriesQuery();
  const [deleteCategory] = useDeleteCategoryMutation();
  const [formOpen, setFormOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<Category | null>(null);

  const treeData = buildCategoryTree(categories ?? []);
  const allParentKeys = collectParentKeys(treeData);

  const handleDelete = async (id: number) => {
    try {
      await deleteCategory(id).unwrap();
    } catch (err) {
      const appError = err as AppError;
      message.error(
        appError.status === 409
          ? 'Cannot delete: this category still has active products referencing it.'
          : appError.message,
      );
    }
  };

  const columns = [
    { title: 'Name', dataIndex: 'name', key: 'name' },
    { title: 'Slug', dataIndex: 'slug', key: 'slug' },
    { title: 'Active', dataIndex: 'isActive', key: 'isActive', render: (v: boolean) => (v ? 'Yes' : 'No') },
    {
      title: '',
      key: 'actions',
      render: (_: unknown, record: Category) => (
        <>
          <Button
            type="link"
            onClick={() => {
              setEditingCategory(record);
              setFormOpen(true);
            }}
          >
            Edit
          </Button>
          <Popconfirm title="Delete this category?" onConfirm={() => handleDelete(record.id)}>
            <Button type="link" danger>
              Delete
            </Button>
          </Popconfirm>
        </>
      ),
    },
  ];

  return (
    <div>
      <Button
        type="primary"
        onClick={() => {
          setEditingCategory(null);
          setFormOpen(true);
        }}
        style={{ marginBottom: 16 }}
      >
        + New Category
      </Button>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={treeData}
        loading={isLoading}
        pagination={false}
        expandable={{ expandedRowKeys: allParentKeys }}
      />
      <Modal open={formOpen} onCancel={() => setFormOpen(false)} footer={null} title={editingCategory ? 'Edit Category' : 'New Category'} destroyOnHidden>
        <CategoryForm category={editingCategory} categories={categories ?? []} onDone={() => setFormOpen(false)} />
      </Modal>
    </div>
  );
}
