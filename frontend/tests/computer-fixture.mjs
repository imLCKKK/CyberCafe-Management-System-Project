export function computerFixture() {
  return {
    permissions: ['computer.view', 'computer.status.update'],
    computers: [
      { computerId: 101, computerName: 'API-PC-01', roomId: 11, computerTypeId: 21, status: 'Available', hasOpenSession: false, hasOpenMaintenance: false },
      { computerId: 102, computerName: 'API-PC-02', roomId: 12, computerTypeId: 22, status: 'Occupied', hasOpenSession: true, hasOpenMaintenance: false },
      { computerId: 103, computerName: 'API-PC-03', roomId: 11, computerTypeId: 21, status: 'Maintenance', hasOpenSession: false, hasOpenMaintenance: true },
      { computerId: 104, computerName: 'API-PC-04', roomId: 12, computerTypeId: 22, status: 'LegacyOffline', hasOpenSession: false, hasOpenMaintenance: false },
    ],
    types: [
      { computerTypeId: 21, typeName: 'API Standard', cpu: null, ram: null, gpu: null, pricePerHour: 12345, description: null },
      { computerTypeId: 22, typeName: 'API Premium', cpu: 'API CPU', ram: '32 GB', gpu: 'API GPU', pricePerHour: 23456, description: 'Premium' },
    ],
    rooms: [
      { roomId: 11, roomName: 'API Room A', floor: 1, description: null },
      { roomId: 12, roomName: 'API Room B', floor: 2, description: null },
    ],
  };
}
