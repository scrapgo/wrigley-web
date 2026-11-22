// Single source of truth for supplier field explanations (tooltips / info bubbles).
//
// Each value is the placeholder text shown for this release: the field's own
// name. Final wording comes from Dale and Kaitlin during QA — change the strings
// HERE, never in the components, so every tooltip updates in one place.

export const SUPPLIER_FIELD_TOOLTIPS = {
    // Payment summary (Dashboard + Payments stat cards)
    totalPaidYtd: 'Total Paid (YTD)',
    paidYtdCount: 'Loads Settled (YTD)',
    pendingPayment: 'Pending Payment',
    pendingPaymentCount: 'Pending Loads',
    loadsLast90Days: 'Loads (Last 90 Days)',
    deliveredLast90DaysAmount: 'Paid (Last 90 Days)',
    deliveredBefore90Days: 'Loads (Before 90 Days)',
    deliveredBefore90DaysAmount: 'Paid (Before 90 Days)',
    currentOffer: 'Current Offer',
    currentOfferSource: 'Offer Source',
    lastDeliveredDate: 'Last Delivered Date',
    lastLoadNumber: 'Last Load #',
    asOf: 'As Of',
    paymentTerms: 'Payment Terms',
    totalActivities: 'Total Activities',

    // Load / dispatch fields
    loadNumber: 'Load #',
    material: 'Material',
    weight: 'Weight (NT)',
    shipDate: 'Ship Date',
    deliveryDate: 'Delivery Date',
    consumer: 'Consumer',
    status: 'Status',
    paymentStatus: 'Payment Status',
    paymentAmount: 'Payment Amount',
    paymentDate: 'Payment Date',
    buyPrice: 'Buy Price',
    salePrice: 'Sale Price',
    grossProfit: 'Gross Profit',
    totalCommissions: 'Total Commissions',
    totalFreightCommissionExpense: 'Freight Commission Expense',
    carrierFee: 'Carrier Fee',
    deadFreight: 'Dead Freight',
    supplierIsDeadFreightExempt: 'Dead Freight Exempt',
    totalGrossSalesRevenue: 'Total Gross Sales Revenue',
    scaleTicketNumber: 'Scale Ticket #',
    shipperPoNumbers: 'Shipper PO #s',
    equipmentType: 'Equipment Type',
    carrierDriver: 'Carrier / Driver',
    minimumWeight: 'Minimum Weight',
    totalGrossWt: 'Total Gross Weight',
    totalTareWt: 'Total Tare Weight',
    totalNetWt: 'Total Net Weight',
    totalDeadFreightViaCietrade: 'Dead Freight via Cietrade',
    deadFreightRequirements: 'Dead Freight Requirements',
    deadFreightChargeExceptions: 'Dead Freight Charge Exceptions',
    weightInGt: 'Weight in GT',
    payDateOverride: 'Pay Date Override',
    originationCity: 'Origination City',
    originationState: 'Origination State',
    pickUpTime: 'Pick Up Time',

    // Target pricing
    targetMaterial: 'Target Material',
    targetBreakEven: 'Target Break Even',
    targetOffer: 'Target Offer',
    targetUom: 'Target UOM',
    trucksPerWeek: 'Trucks / Week',
    targetFreightPerUom: 'Target FR / UOM',
    targetFreightCost: 'Target Freight Cost',
    targetConsumerPrice: 'Target Consumer Price',
    priceInNetTons: 'Price in Net Tons',
    priceInLbs: 'Price in LBS',
    priceInCwt: 'Price in CWT',
    priceInGrossTons: 'Price in Gross Tons',
    targetPoNumber: 'Target PO Number',
    priceChangeFromPrior: 'Price Change from Prior',

    // In-stock items
    materialType: 'Material Type',
    packaging: 'Packaging',
    unitOfMeasure: 'UOM',
    truckType: 'Truck Type',
    volume: 'Volume',
} as const

export type SupplierTooltipKey = keyof typeof SUPPLIER_FIELD_TOOLTIPS