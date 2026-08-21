import { Upload, Button, message } from 'antd';
import type { UploadProps } from 'antd';
import { useUploadImageMutation, useDeleteImageMutation } from './api';
import type { AppError } from '../../shared/lib/errors';

interface Props {
  productId: number;
  imageUrl: string | null;
}

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_SIZE_BYTES = 5 * 1024 * 1024;

export function ImageUploader({ productId, imageUrl }: Props) {
  const [uploadImage, { isLoading: uploading }] = useUploadImageMutation();
  const [deleteImage] = useDeleteImageMutation();

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!ALLOWED_TYPES.includes(file.type)) {
      message.error('Only JPEG, PNG, or WEBP images are allowed.');
      return Upload.LIST_IGNORE;
    }
    if (file.size > MAX_SIZE_BYTES) {
      message.error('Image must be 5 MB or smaller.');
      return Upload.LIST_IGNORE;
    }
    return true;
  };

  const customRequest: UploadProps['customRequest'] = async (options) => {
    try {
      const formData = new FormData();
      formData.append('file', options.file as Blob);
      await uploadImage({ productId, formData }).unwrap();
      options.onSuccess?.({});
    } catch (err) {
      message.error((err as AppError).message);
      options.onError?.(err as Error);
    }
  };

  async function handleRemove() {
    try {
      await deleteImage(productId).unwrap();
    } catch (err) {
      message.error((err as AppError).message);
    }
  }

  if (imageUrl) {
    return (
      <div>
        <img
          src={imageUrl}
          alt="Product"
          style={{ width: 120, height: 120, objectFit: 'cover' }}
          onError={(e) => {
            (e.target as HTMLImageElement).style.visibility = 'hidden'; // broken-image fallback (spec section 8)
          }}
        />
        <Button size="small" onClick={handleRemove} style={{ display: 'block', marginTop: 4 }}>
          Remove
        </Button>
      </div>
    );
  }

  return (
    <Upload beforeUpload={beforeUpload} customRequest={customRequest} showUploadList={false}>
      <Button loading={uploading}>Upload Image</Button>
    </Upload>
  );
}
