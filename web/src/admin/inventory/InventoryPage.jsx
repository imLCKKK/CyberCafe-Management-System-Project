import React, { useState, useMemo } from 'react';
import {
  INITIAL_INVENTORY_ITEMS,
  calculateInventoryStats,
} from './mockData';
import { InventoryStats } from './components/InventoryStats';
import { InventoryToolbar } from './components/InventoryToolbar';
import { InventoryTable } from './components/InventoryTable';
import { ItemModal } from './components/ItemModal';
import { StockImportModal } from './components/StockImportModal';
import { DeleteModal } from './components/DeleteModal';

export function InventoryPage() {
  const [items, setItems] = useState(INITIAL_INVENTORY_ITEMS);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('Tất cả');
  const [statusFilter, setStatusFilter] = useState('all');

  // Modals state
  const [isItemModalOpen, setIsItemModalOpen] = useState(false);
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [activeItem, setActiveItem] = useState(null);

  // Compute stats
  const stats = useMemo(() => calculateInventoryStats(items), [items]);

  // Filtered items
  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      // Category filter
      if (selectedCategory !== 'Tất cả' && item.category !== selectedCategory) {
        return false;
      }

      // Status filter
      if (statusFilter !== 'all' && item.status !== statusFilter) {
        return false;
      }

      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const matchName = item.name.toLowerCase().includes(query);
        const matchId = item.id.toLowerCase().includes(query);
        if (!matchName && !matchId) return false;
      }

      return true;
    });
  }, [items, selectedCategory, statusFilter, searchQuery]);

  // Handlers
  const handleOpenAddItem = () => {
    setActiveItem(null);
    setIsItemModalOpen(true);
  };

  const handleOpenEditItem = (item) => {
    setActiveItem(item);
    setIsItemModalOpen(true);
  };

  const handleOpenImport = (item = null) => {
    setActiveItem(item);
    setIsImportModalOpen(true);
  };

  const handleOpenDelete = (item) => {
    setActiveItem(item);
    setIsDeleteModalOpen(true);
  };

  const handleSaveItem = (itemData) => {
    const computeStatus = (stock) => {
      if (stock === 0) return 'out_of_stock';
      if (stock <= 5) return 'low_stock';
      return 'in_stock';
    };

    if (itemData.id) {
      // Edit
      setItems((prev) =>
        prev.map((i) =>
          i.id === itemData.id
            ? {
                ...i,
                ...itemData,
                status: computeStatus(itemData.stock),
              }
            : i
        )
      );
    } else {
      // Create new
      const nextNum = items.length + 1;
      const newId = `SP${String(nextNum).padStart(3, '0')}`;
      const newItem = {
        ...itemData,
        id: newId,
        status: computeStatus(itemData.stock),
      };
      setItems((prev) => [newItem, ...prev]);
    }

    setIsItemModalOpen(false);
  };

  const handleImportStock = (id, quantity) => {
    setItems((prev) =>
      prev.map((i) => {
        if (i.id === id) {
          const newStock = i.stock + quantity;
          let newStatus = 'in_stock';
          if (newStock === 0) newStatus = 'out_of_stock';
          else if (newStock <= 5) newStatus = 'low_stock';

          return {
            ...i,
            stock: newStock,
            status: newStatus,
          };
        }
        return i;
      })
    );
    setIsImportModalOpen(false);
  };

  const handleDeleteItem = (id) => {
    setItems((prev) => prev.filter((i) => i.id !== id));
    setIsDeleteModalOpen(false);
  };

  return (
    <div className="page" style={{ display: 'block' }}>
      {/* Module Title */}
      <div style={{ marginBottom: '18px' }}>
        <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>
          Quản lý kho hàng
        </h2>
        <p style={{ fontSize: '12px', color: '#8993A4', margin: 0 }}>
          Theo dõi tồn kho thực tế, nhập hàng và định giá bán tại phòng máy
        </p>
      </div>

      {/* KPI Stats */}
      <InventoryStats
        stats={stats}
        statusFilter={statusFilter}
        setStatusFilter={setStatusFilter}
      />

      {/* Toolbar */}
      <InventoryToolbar
        searchQuery={searchQuery}
        setSearchQuery={setSearchQuery}
        selectedCategory={selectedCategory}
        setSelectedCategory={setSelectedCategory}
        onOpenAddItem={handleOpenAddItem}
        onOpenImport={() => handleOpenImport(null)}
      />

      {/* Table */}
      <InventoryTable
        items={filteredItems}
        onEdit={handleOpenEditItem}
        onImport={handleOpenImport}
        onDelete={handleOpenDelete}
      />

      {/* Modals */}
      <ItemModal
        isOpen={isItemModalOpen}
        item={activeItem}
        onClose={() => setIsItemModalOpen(false)}
        onSave={handleSaveItem}
      />

      <StockImportModal
        isOpen={isImportModalOpen}
        item={activeItem}
        allItems={items}
        onClose={() => setIsImportModalOpen(false)}
        onImport={handleImportStock}
      />

      <DeleteModal
        isOpen={isDeleteModalOpen}
        item={activeItem}
        onClose={() => setIsDeleteModalOpen(false)}
        onConfirm={handleDeleteItem}
      />
    </div>
  );
}

export default InventoryPage;
