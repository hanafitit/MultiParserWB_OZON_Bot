const { WBPrivateAPI } = require('wb-private-api');
const Constants = require('wb-private-api/src/Constants');

const api = new WBPrivateAPI({ destination: Constants.DESTINATIONS.MOSCOW });

async function getProduct(sku) {
    try {
        const products = await api.getListOfProducts([sku]);

        if (!products || products.length === 0) {
            console.error('Product not found');
            process.exit(1);
        }

        const product = products[0];
        const stocks = product.sizes ? product.sizes.reduce((acc, size) => {
            return acc + (size.stocks ? size.stocks.reduce((sAcc, s) => sAcc + s.qty, 0) : 0);
        }, 0) : 0;

        const result = {
            id: product.id,
            name: product.name,
            price: product.salePriceU / 100,
            salePrice: product.priceU / 100,
            rating: product.reviewRating,
            feedbacks: product.feedbacks,
            stocks: stocks
        };

        console.log(JSON.stringify(result));
    } catch (error) {
        if (error.response && error.response.status === 403) {
            console.error('Error fetching product: WB blocked the request (403). Try again later or use proxy.');
        } else {
            console.error('Error fetching product:', error.message);
        }
        process.exit(1);
    }
}

const sku = process.argv[2];
if (!sku) {
    console.error('Please provide an SKU');
    process.exit(1);
}

getProduct(sku);
