import { Form, Input, InputNumber, TreeSelect, Button, message } from 'antd';
import { useCreateCategoryMutation, useUpdateCategoryMutation } from './api';
import type { Category } from './types';
import { buildCategoryTree } from './buildCategoryTree';
import type { CategoryTreeNode } from './buildCategoryTree';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  category: Category | null;
  categories: Category[];
  onDone: () => void;
}

interface TreeSelectNode {
  title: string;
  value: number;
  children?: TreeSelectNode[];
}

function toTreeSelectNode(node: CategoryTreeNode): TreeSelectNode {
  return { title: node.name, value: node.id, children: node.children?.map(toTreeSelectNode) };
}

export function CategoryForm({ category, categories, onDone }: Props) {
  const [form] = Form.useForm();
  const [createCategory, { isLoading: creating }] = useCreateCategoryMutation();
  const [updateCategory, { isLoading: updating }] = useUpdateCategoryMutation();

  const treeData = buildCategoryTree(categories.filter((c) => c.id !== category?.id)).map(toTreeSelectNode);

  const handleFinish = async (values: { name: string; slug: string; parentCategoryId?: number; displayOrder: number }) => {
    try {
      const parentCategoryId = values.parentCategoryId ?? null;
      if (category) {
        await updateCategory({ id: category.id, body: { ...values, parentCategoryId, isActive: category.isActive } }).unwrap();
      } else {
        await createCategory({ ...values, parentCategoryId }).unwrap();
      }
      onDone();
    } catch (err) {
      const appError = err as AppError;
      if (appError.fieldErrors) {
        form.setFields(
          appError.fieldErrors.map((fe) => ({
            name: fe.propertyName.charAt(0).toLowerCase() + fe.propertyName.slice(1),
            errors: [fe.errorMessage],
          })),
        );
      } else {
        message.error(appError.message);
      }
    }
  };

  return (
    <Form form={form} layout="vertical" initialValues={category ?? { displayOrder: 0 }} onFinish={handleFinish}>
      <Form.Item name="name" label="Name" rules={[{ required: true, max: 120 }]}>
        <Input />
      </Form.Item>
      <Form.Item
        name="slug"
        label="Slug"
        rules={[{ required: true, pattern: /^[a-z0-9-]+$/, message: 'Lowercase letters, numbers, and hyphens only.' }]}
      >
        <Input />
      </Form.Item>
      <Form.Item name="parentCategoryId" label="Parent Category">
        <TreeSelect treeData={treeData} allowClear placeholder="None (top-level)" />
      </Form.Item>
      <Form.Item name="displayOrder" label="Display Order">
        <InputNumber style={{ width: '100%' }} />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={creating || updating}>
        Save
      </Button>
    </Form>
  );
}
